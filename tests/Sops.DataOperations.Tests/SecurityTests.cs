using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Security;
using Xunit;

namespace Sops.DataOperations.Tests
{
    public class SecurityTests
    {
        [Fact]
        public void BackupKeyProvider_GenerateRandomMasterSecret_ReturnsValidBase64()
        {
            string secret = BackupKeyProvider.GenerateRandomMasterSecret();
            Assert.False(string.IsNullOrWhiteSpace(secret));
            byte[] bytes = Convert.FromBase64String(secret);
            Assert.Equal(32, bytes.Length);
        }

        [Fact]
        public void BackupKeyProvider_DeriveKeys_DeterministicAndSeparated()
        {
            string secret = "test-master-secret-for-sops-data-ops-12345";
            var keys1 = BackupKeyProvider.DeriveKeys(secret, "primary");
            var keys2 = BackupKeyProvider.DeriveKeys(secret, "primary");

            Assert.Equal(keys1.EncryptionKey, keys2.EncryptionKey);
            Assert.Equal(keys1.AuthenticationKey, keys2.AuthenticationKey);
            Assert.Equal("primary", keys1.KeyIdentifier);

            // Encryption key and Authentication key must be completely different
            Assert.NotEqual(keys1.EncryptionKey, keys1.AuthenticationKey);
            Assert.Equal(32, keys1.EncryptionKey.Length);
            Assert.Equal(32, keys1.AuthenticationKey.Length);

            // Different identifier yields different keys
            var secondaryKeys = BackupKeyProvider.DeriveKeys(secret, "secondary");
            Assert.NotEqual(keys1.EncryptionKey, secondaryKeys.EncryptionKey);
            Assert.NotEqual(keys1.AuthenticationKey, secondaryKeys.AuthenticationKey);
        }

        [Fact]
        public async Task EncryptedStreamProcessor_Roundtrip_MatchesOriginalPayload()
        {
            var keys = BackupKeyProvider.DeriveKeys("strong-passphrase-roundtrip-test", "v1");
            byte[] originalData = Encoding.UTF8.GetBytes("SOPs Pharmacy Database Snapshot Payload Content 1234567890!");

            var manifest = new BackupManifest
            {
                BackupId = Guid.NewGuid(),
                PharmacyId = Guid.NewGuid(),
                FormatVersion = 1,
                DatabaseSchemaVersion = "1.0.0",
                ApplicationVersion = "1.0.0",
                EncryptionAlgorithm = "AES-256-CBC+HMAC-SHA256",
                KeyIdentifier = keys.KeyIdentifier,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            using var inputStream = new MemoryStream(originalData);
            using var encryptedStream = new MemoryStream();

            // Encrypt
            await EncryptedStreamProcessor.EncryptStreamAsync(inputStream, encryptedStream, keys, manifest);

            encryptedStream.Position = 0;
            using var decryptedStream = new MemoryStream();

            // Decrypt
            await EncryptedStreamProcessor.DecryptStreamAsync(encryptedStream, decryptedStream, keys);

            byte[] decryptedData = decryptedStream.ToArray();
            Assert.Equal(originalData, decryptedData);
        }

        [Fact]
        public async Task EncryptedStreamProcessor_TamperedPayload_FailsHmacVerification()
        {
            var keys = BackupKeyProvider.DeriveKeys("strong-passphrase-roundtrip-test", "v1");
            byte[] originalData = Encoding.UTF8.GetBytes("Critical SQLite Data Block");

            var manifest = new BackupManifest
            {
                BackupId = Guid.NewGuid(),
                PharmacyId = Guid.NewGuid(),
                FormatVersion = 1,
                DatabaseSchemaVersion = "1.0.0",
                ApplicationVersion = "1.0.0",
                EncryptionAlgorithm = "AES-256-CBC+HMAC-SHA256",
                KeyIdentifier = keys.KeyIdentifier,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            using var inputStream = new MemoryStream(originalData);
            using var encryptedStream = new MemoryStream();

            await EncryptedStreamProcessor.EncryptStreamAsync(inputStream, encryptedStream, keys, manifest);

            // Tamper with encrypted stream bytes (modify a byte in the payload)
            byte[] tamperedBytes = encryptedStream.ToArray();
            tamperedBytes[tamperedBytes.Length / 2] ^= 0xFF;

            using var tamperedStream = new MemoryStream(tamperedBytes);
            using var decryptedStream = new MemoryStream();

            await Assert.ThrowsAsync<CryptographicException>(() =>
                EncryptedStreamProcessor.DecryptStreamAsync(tamperedStream, decryptedStream, keys));
        }

        [Fact]
        public async Task EncryptedStreamProcessor_WrongKey_FailsDecryption()
        {
            var keys1 = BackupKeyProvider.DeriveKeys("secret-one", "k1");
            var keys2 = BackupKeyProvider.DeriveKeys("secret-two", "k1");
            byte[] originalData = Encoding.UTF8.GetBytes("Sample sensitive database bytes");

            var manifest = new BackupManifest
            {
                BackupId = Guid.NewGuid(),
                PharmacyId = Guid.NewGuid(),
                FormatVersion = 1,
                DatabaseSchemaVersion = "1.0.0",
                ApplicationVersion = "1.0.0",
                EncryptionAlgorithm = "AES-256-CBC+HMAC-SHA256",
                KeyIdentifier = keys1.KeyIdentifier,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            using var inputStream = new MemoryStream(originalData);
            using var encryptedStream = new MemoryStream();

            await EncryptedStreamProcessor.EncryptStreamAsync(inputStream, encryptedStream, keys1, manifest);

            encryptedStream.Position = 0;
            using var decryptedStream = new MemoryStream();

            await Assert.ThrowsAsync<CryptographicException>(() =>
                EncryptedStreamProcessor.DecryptStreamAsync(encryptedStream, decryptedStream, keys2));
        }

        [Fact]
        public void OperationAuthorization_DemandPermission_ValidatesAccess()
        {
            var user = new UserIdentity
            {
                UserId = Guid.NewGuid(),
                Username = "admin_user",
                Roles = new List<string> { "Administrator" },
                Permissions = new List<string> { Permissions.BackupCreate, Permissions.RestoreExecute }
            };

            // Granted permission should not throw
            OperationAuthorization.DemandPermission(user, Permissions.BackupCreate, "BackupOperation");

            // Missing permission should throw
            var ex = Assert.Throws<SecurityException>(() =>
                OperationAuthorization.DemandPermission(user, Permissions.ApprovalApprove, "ApprovalOperation"));
            Assert.Contains(Permissions.ApprovalApprove, ex.Message);
        }

        [Fact]
        public void OperationAuthorization_SeparationOfDuties_Enforced()
        {
            var requesterId = Guid.NewGuid();
            var reviewerId = Guid.NewGuid();

            var sameUser = new UserIdentity { UserId = requesterId, Username = "same_user" };
            var differentUser = new UserIdentity { UserId = reviewerId, Username = "diff_user" };

            // Same user cannot review own request
            var ex = Assert.Throws<SecurityException>(() =>
                OperationAuthorization.DemandSeparationOfDuties(sameUser, requesterId, "SelfApprovalCheck"));
            Assert.Contains("Separation of duties violation", ex.Message);

            // Different user can review without throwing
            OperationAuthorization.DemandSeparationOfDuties(differentUser, requesterId, "DifferentUserCheck");
        }

        [Fact]
        public void OperationAuthorization_RequiresDualApproval_EvaluatesRiskTiers()
        {
            Assert.True(OperationAuthorization.RequiresDualApproval(ChangeRiskTier.Critical, "AnyChange"));
            Assert.True(OperationAuthorization.RequiresDualApproval(ChangeRiskTier.High, "AnyChange"));
            Assert.True(OperationAuthorization.RequiresDualApproval(ChangeRiskTier.Standard, "PriceRegulatedOverride"));
            Assert.True(OperationAuthorization.RequiresDualApproval(ChangeRiskTier.Standard, "StockWriteOff"));

            Assert.False(OperationAuthorization.RequiresDualApproval(ChangeRiskTier.Standard, "MinorMetadataUpdate"));
            Assert.False(OperationAuthorization.RequiresDualApproval(ChangeRiskTier.Elevated, "MinorMetadataUpdate"));
        }
    }
}
