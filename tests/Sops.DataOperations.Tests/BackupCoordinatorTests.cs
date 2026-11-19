using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Backup;
using Sops.DataOperations.Security;
using Xunit;

namespace Sops.DataOperations.Tests
{
    public class BackupCoordinatorTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _dummyDbPath;
        private readonly string _backupOutputDir;
        private readonly EncryptedBackupCoordinator _coordinator;
        private readonly BackupVerificationService _verifier;
        private readonly BackupRetentionManager _retentionManager;

        public BackupCoordinatorTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "sops_backup_tests_" + Guid.NewGuid().ToString("N"));
            _backupOutputDir = Path.Combine(_testDir, "backups");
            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(_backupOutputDir);

            _dummyDbPath = Path.Combine(_testDir, "test_pharmacy.db");
            // Write some pseudo-sqlite database bytes
            byte[] pseudoSqliteHeader = Encoding.UTF8.GetBytes("SQLite format 3\0");
            byte[] dummyData = new byte[1024 * 16]; // 16 KB dummy db
            Array.Copy(pseudoSqliteHeader, dummyData, pseudoSqliteHeader.Length);
            for (int i = pseudoSqliteHeader.Length; i < dummyData.Length; i++)
            {
                dummyData[i] = (byte)(i % 256);
            }
            File.WriteAllBytes(_dummyDbPath, dummyData);

            _verifier = new BackupVerificationService(NullLogger<BackupVerificationService>.Instance);
            _retentionManager = new BackupRetentionManager(NullLogger<BackupRetentionManager>.Instance);
            var policy = new BackupPolicy
            {
                DefaultOutputDirectory = _backupOutputDir,
                MinimumFreeDiskSpaceBytes = 1024 // Low threshold for test
            };

            _coordinator = new EncryptedBackupCoordinator(
                _verifier,
                _retentionManager,
                policy,
                NullLogger<EncryptedBackupCoordinator>.Instance);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch
            {
                // Ignore test cleanup exceptions
            }
        }

        [Fact]
        public async Task CreateBackupAsync_GeneratesAuthenticatedBackupArchive()
        {
            string secret = "sops-master-backup-secret-key-xyz-777";
            var pharmacyId = Guid.NewGuid();
            var chainId = Guid.NewGuid();

            var request = new BackupRequest
            {
                PharmacyId = pharmacyId,
                ChainId = chainId,
                InstallationId = "POS-TERMINAL-01",
                SourceDatabasePath = _dummyDbPath,
                OutputDirectory = _backupOutputDir,
                BackupKeySecret = secret,
                DatabaseSchemaVersion = "1.0.0",
                ApplicationVersion = "2.5.0",
                CurrentRecoveryGeneration = Guid.NewGuid(),
                CurrentCheckpoint = 100
            };

            var result = await _coordinator.CreateBackupAsync(request);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.True(File.Exists(result.Data.ArchivePath));
            Assert.NotNull(result.Data.Manifest);
            Assert.True(result.Data.Manifest.ArchiveSizeBytes > 0);
            Assert.Equal(pharmacyId, result.Data.Manifest.PharmacyId);

            // Verify with verifier service using correct secret
            var verifyResult = await _verifier.VerifyArchiveAsync(result.Data.ArchivePath, secret);
            Assert.True(verifyResult.IsValid);
            Assert.NotNull(verifyResult.Manifest);
            Assert.Equal(pharmacyId, verifyResult.Manifest.PharmacyId);
            Assert.Equal(chainId, verifyResult.Manifest.ChainId);
            Assert.Equal("1.0.0", verifyResult.Manifest.DatabaseSchemaVersion);
        }

        [Fact]
        public async Task VerifyArchiveAsync_WrongSecret_FailsIntegrityCheck()
        {
            string secret = "valid-secret-key-1";
            string wrongSecret = "wrong-secret-key-2";

            var request = new BackupRequest
            {
                PharmacyId = Guid.NewGuid(),
                SourceDatabasePath = _dummyDbPath,
                OutputDirectory = _backupOutputDir,
                BackupKeySecret = secret,
                DatabaseSchemaVersion = "1.0.0"
            };

            var result = await _coordinator.CreateBackupAsync(request);
            Assert.True(result.Success);

            // Verifying with wrong secret fails HMAC constant-time comparison
            var verifyResult = await _verifier.VerifyArchiveAsync(result.Data!.ArchivePath, wrongSecret);
            Assert.False(verifyResult.IsValid);
            Assert.Contains("HMAC", verifyResult.ErrorMessage);
        }

        [Fact]
        public async Task VerifyArchiveAsync_TamperedBytes_FailsVerification()
        {
            string secret = "tamper-proof-secret-123";
            var request = new BackupRequest
            {
                PharmacyId = Guid.NewGuid(),
                SourceDatabasePath = _dummyDbPath,
                OutputDirectory = _backupOutputDir,
                BackupKeySecret = secret,
                DatabaseSchemaVersion = "1.0.0"
            };

            var result = await _coordinator.CreateBackupAsync(request);
            string archivePath = result.Data!.ArchivePath;

            // Tamper archive content by corrupting byte in the middle of ciphertext
            byte[] archiveBytes = File.ReadAllBytes(archivePath);
            archiveBytes[archiveBytes.Length - 50] ^= 0xAA;
            File.WriteAllBytes(archivePath, archiveBytes);

            var verifyResult = await _verifier.VerifyArchiveAsync(archivePath, secret);
            Assert.False(verifyResult.IsValid);
        }

        [Fact]
        public async Task VerifyArchiveAsync_CorruptMagic_FailsVerification()
        {
            string corruptFile = Path.Combine(_testDir, "corrupt_magic.sopsbak");
            File.WriteAllBytes(corruptFile, Encoding.ASCII.GetBytes("NOTSOPSBAKCORRUPT"));

            var verifyResult = await _verifier.VerifyArchiveAsync(corruptFile, "secret");
            Assert.False(verifyResult.IsValid);
            Assert.Contains("signature mismatch", verifyResult.ErrorMessage);
        }

        [Fact]
        public async Task VerifyArchiveAsync_TruncatedFile_FailsVerification()
        {
            string truncatedFile = Path.Combine(_testDir, "truncated.sopsbak");
            File.WriteAllBytes(truncatedFile, Encoding.ASCII.GetBytes("SOPSBK01")); // Only 8 bytes

            var verifyResult = await _verifier.VerifyArchiveAsync(truncatedFile, "secret");
            Assert.False(verifyResult.IsValid);
        }

        [Fact]
        public async Task CreateBackupAsync_NonExistentSource_FailsFast()
        {
            var request = new BackupRequest
            {
                PharmacyId = Guid.NewGuid(),
                SourceDatabasePath = Path.Combine(_testDir, "missing_source.db"),
                OutputDirectory = _backupOutputDir,
                BackupKeySecret = "any-key"
            };

            var result = await _coordinator.CreateBackupAsync(request);
            Assert.False(result.Success);
            Assert.Equal("SOURCE_NOT_FOUND", result.ErrorCode);
        }

        [Fact]
        public async Task BackupRetentionManager_EnforcesRetention_PreservesMinimumBackups()
        {
            // Create 5 dummy backup files
            var files = new string[5];
            for (int i = 0; i < 5; i++)
            {
                string path = Path.Combine(_backupOutputDir, $"sops_backup_dummy_{i:D2}.sopsbak");
                File.WriteAllText(path, $"backup-data-{i}");
                File.SetCreationTimeUtc(path, DateTime.UtcNow.AddHours(-10 + i));
                files[i] = path;
            }

            var policy = new BackupRetentionPolicy
            {
                MaxBackupsToKeep = 2,
                AlwaysKeepAtLeastOne = true,
                RetainDays = 30
            };

            int deletedCount = await _retentionManager.EnforceRetentionAsync(_backupOutputDir, policy);

            Assert.Equal(3, deletedCount);

            // Ensure newest files remain
            Assert.True(File.Exists(files[4]));
            Assert.True(File.Exists(files[3]));
            // Ensure oldest files were deleted
            Assert.False(File.Exists(files[0]));
            Assert.False(File.Exists(files[1]));
            Assert.False(File.Exists(files[2]));
        }
    }
}
