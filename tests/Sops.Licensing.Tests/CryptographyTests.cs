using System;
using System.Collections.Generic;
using Sops.Licensing.Cryptography;
using Sops.Licensing.Models;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class CryptographyTests
    {
        [Fact]
        public void KeyGeneration_ProducesValidRsaKeyPair()
        {
            var (privateXml, publicXml, publicPem) = CertificateSigner.GenerateRsaKeyPair(2048);

            Assert.NotEmpty(privateXml);
            Assert.Contains("<RSAKeyValue>", privateXml);
            Assert.Contains("<InverseQ>", privateXml); // Indicates private key

            Assert.NotEmpty(publicXml);
            Assert.Contains("<RSAKeyValue>", publicXml);
            Assert.DoesNotContain("<InverseQ>", publicXml); // Indicates public key only

            Assert.NotEmpty(publicPem);
            Assert.StartsWith("-----BEGIN PUBLIC KEY-----", publicPem);
        }

        [Fact]
        public void CanonicalSerializer_ProducesDeterministicOutput()
        {
            var cert1 = new LicenseCertificate
            {
                CertificateVersion = 1,
                LicenseId = "lic-12345",
                ProductId = "SOPs.Core",
                SubjectId = "ClientA",
                PlanId = "ENTERPRISE",
                IssuedAtUtc = DateTimeOffset.Parse("2026-11-18T00:00:00Z"),
                ValidFromUtc = DateTimeOffset.Parse("2026-11-18T00:00:00Z"),
                ExpiresAtUtc = DateTimeOffset.Parse("2027-11-18T00:00:00Z"),
                OfflineValidUntilUtc = DateTimeOffset.Parse("2026-12-18T00:00:00Z"),
                DeviceBinding = "device_hash_abcdef",
                SigningKeyId = "root_2026",
                Entitlements = new List<string> { "pos.access", "database.raw.read", "app.login" },
                Limits = new Dictionary<string, long> { { "max_users", 25 }, { "max_branches", 5 } }
            };

            var cert2 = new LicenseCertificate
            {
                CertificateVersion = 1,
                LicenseId = "lic-12345",
                ProductId = "SOPs.Core",
                SubjectId = "ClientA",
                PlanId = "ENTERPRISE",
                IssuedAtUtc = DateTimeOffset.Parse("2026-11-18T00:00:00Z"),
                ValidFromUtc = DateTimeOffset.Parse("2026-11-18T00:00:00Z"),
                ExpiresAtUtc = DateTimeOffset.Parse("2027-11-18T00:00:00Z"),
                OfflineValidUntilUtc = DateTimeOffset.Parse("2026-12-18T00:00:00Z"),
                DeviceBinding = "device_hash_abcdef",
                SigningKeyId = "root_2026",
                // Unordered inputs
                Entitlements = new List<string> { "app.login", "database.raw.read", "pos.access" },
                Limits = new Dictionary<string, long> { { "max_branches", 5 }, { "max_users", 25 } }
            };

            byte[] b1 = CanonicalSerializer.SerializeForSignature(cert1);
            byte[] b2 = CanonicalSerializer.SerializeForSignature(cert2);

            Assert.Equal(b1, b2);
        }

        [Fact]
        public void SignAndVerify_ValidCertificate_VerifiesSuccessfully()
        {
            var (privateXml, publicXml, publicPem) = CertificateSigner.GenerateRsaKeyPair();

            var cert = new LicenseCertificate
            {
                LicenseId = "lic-verified-001",
                ProductId = "SOPs.App",
                SubjectId = "EnterpriseCustomer",
                PlanId = "GOLD",
                IssuedAtUtc = DateTimeOffset.UtcNow,
                ValidFromUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddYears(1),
                OfflineValidUntilUtc = DateTimeOffset.UtcNow.AddDays(30),
                SigningKeyId = "key-prod-01",
                Entitlements = new List<string> { "database.*", "app.login" }
            };

            CertificateSigner.SignCertificate(cert, privateXml);
            Assert.NotEmpty(cert.Signature);

            // Verify with XML public key
            var trustedXml = new Dictionary<string, string> { { "key-prod-01", publicXml } };
            Assert.True(CertificateVerifier.VerifySignature(cert, trustedXml));

            // Verify with PEM public key
            var trustedPem = new Dictionary<string, string> { { "key-prod-01", publicPem } };
            Assert.True(CertificateVerifier.VerifySignature(cert, trustedPem));
        }

        [Fact]
        public void VerifySignature_TamperedPayload_FailsVerification()
        {
            var (privateXml, publicXml, _) = CertificateSigner.GenerateRsaKeyPair();

            var cert = new LicenseCertificate
            {
                LicenseId = "lic-tamper-001",
                ProductId = "SOPs.App",
                SigningKeyId = "key-01",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
                Entitlements = new List<string> { "database.read" }
            };

            CertificateSigner.SignCertificate(cert, privateXml);
            var trusted = new Dictionary<string, string> { { "key-01", publicXml } };

            // Verify original
            Assert.True(CertificateVerifier.VerifySignature(cert, trusted));

            // Tamper with entitlement
            cert.Entitlements.Add("database.write");
            Assert.False(CertificateVerifier.VerifySignature(cert, trusted));

            // Tamper with expiration date
            cert.Entitlements.Remove("database.write");
            cert.ExpiresAtUtc = cert.ExpiresAtUtc.AddYears(10);
            Assert.False(CertificateVerifier.VerifySignature(cert, trusted));
        }

        [Fact]
        public void VerifySignature_WrongKey_FailsVerification()
        {
            var (privateXml1, _, _) = CertificateSigner.GenerateRsaKeyPair();
            var (_, publicXml2, _) = CertificateSigner.GenerateRsaKeyPair();

            var cert = new LicenseCertificate
            {
                LicenseId = "lic-wrongkey-001",
                ProductId = "SOPs.App",
                SigningKeyId = "key-01"
            };

            CertificateSigner.SignCertificate(cert, privateXml1);

            // Try verifying with a different key
            var trusted = new Dictionary<string, string> { { "key-01", publicXml2 } };
            Assert.False(CertificateVerifier.VerifySignature(cert, trusted));
        }

        [Fact]
        public void ConstantTime_Equals_ReturnsCorrectBoolean()
        {
            byte[] a = new byte[] { 1, 2, 3, 4, 5 };
            byte[] b = new byte[] { 1, 2, 3, 4, 5 };
            byte[] c = new byte[] { 1, 2, 3, 4, 6 };
            byte[] d = new byte[] { 1, 2, 3 };

            Assert.True(ConstantTime.Equals(a, b));
            Assert.False(ConstantTime.Equals(a, c));
            Assert.False(ConstantTime.Equals(a, d));
            Assert.False(ConstantTime.Equals(a, null));
            Assert.False(ConstantTime.Equals(null, b));
            Assert.True(ConstantTime.Equals((byte[]?)null, null));
        }
    }
}
