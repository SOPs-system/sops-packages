using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Backup;
using Sops.DataOperations.Restore;
using Sops.DataOperations.Security;
using Sops.DataOperations.Sync;
using Sops.DataOperations.Sync.Storage;
using Xunit;

namespace Sops.DataOperations.Tests
{
    public class RestoreCoordinatorTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _backupDir;
        private readonly string _liveDbPath;
        private readonly BackupVerificationService _verifier;
        private readonly EncryptedBackupCoordinator _backupCoordinator;
        private readonly RestorePreflight _preflight;
        private readonly RestoreStagingService _stagingService;
        private readonly RestoreActivationService _activationService;
        private readonly PostRestoreSyncCoordinator _postRestoreSync;
        private readonly RestoreCoordinator _restoreCoordinator;

        private readonly UserIdentity _authorizedAdmin;
        private readonly string _secret = "secure-restore-test-secret-999";
        private readonly Guid _pharmacyId = Guid.NewGuid();
        private readonly Guid _chainId = Guid.NewGuid();

        public RestoreCoordinatorTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "sops_restore_tests_" + Guid.NewGuid().ToString("N"));
            _backupDir = Path.Combine(_testDir, "backups");
            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(_backupDir);

            _liveDbPath = Path.Combine(_testDir, "live_pharmacy.db");
            File.WriteAllText(_liveDbPath, "INITIAL_DATABASE_STATE_ORIGINAL_REVISION_1");

            _verifier = new BackupVerificationService(NullLogger<BackupVerificationService>.Instance);
            var retentionManager = new BackupRetentionManager(NullLogger<BackupRetentionManager>.Instance);
            var policy = new BackupPolicy { MinimumFreeDiskSpaceBytes = 1024, DefaultOutputDirectory = _backupDir };

            _backupCoordinator = new EncryptedBackupCoordinator(_verifier, retentionManager, policy);
            _preflight = new RestorePreflight(_verifier, NullLogger<RestorePreflight>.Instance);
            _stagingService = new RestoreStagingService(NullLogger<RestoreStagingService>.Instance);
            _activationService = new RestoreActivationService(NullLogger<RestoreActivationService>.Instance);

            var checkpoints = new SyncCheckpointCoordinator();
            var outbox = new InMemorySyncStore();
            var inbox = new InMemorySyncStore();
            var transport = Substitute.For<ISyncTransport>();
            var conflictHandler = new SyncConflictHandler();
            _postRestoreSync = new PostRestoreSyncCoordinator(checkpoints, outbox, inbox, transport, conflictHandler, NullLogger<PostRestoreSyncCoordinator>.Instance);

            _restoreCoordinator = new RestoreCoordinator(
                _preflight,
                _stagingService,
                _activationService,
                _postRestoreSync,
                NullLogger<RestoreCoordinator>.Instance);

            _authorizedAdmin = new UserIdentity
            {
                UserId = Guid.NewGuid(),
                Username = "chief_administrator",
                Permissions = new List<string> { Permissions.RestoreExecute }
            };
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
                // Ignore cleanup errors
            }
        }

        private async Task<string> CreateValidBackupArchiveAsync()
        {
            var req = new BackupRequest
            {
                PharmacyId = _pharmacyId,
                ChainId = _chainId,
                InstallationId = "TERM-101",
                SourceDatabasePath = _liveDbPath,
                OutputDirectory = _backupDir,
                BackupKeySecret = _secret,
                DatabaseSchemaVersion = "2.0.0",
                ApplicationVersion = "1.0.0",
                CurrentRecoveryGeneration = Guid.NewGuid(),
                CurrentCheckpoint = 50
            };

            var res = await _backupCoordinator.CreateBackupAsync(req);
            Assert.True(res.Success);
            return res.Data!.ArchivePath;
        }

        [Fact]
        public async Task PreflightCheck_DetectsPharmacyMismatch()
        {
            string archivePath = await CreateValidBackupArchiveAsync();

            var request = new RestoreRequest
            {
                ArchiveFilePath = archivePath,
                TargetDatabasePath = _liveDbPath,
                BackupKeySecret = _secret,
                ExpectedPharmacyId = Guid.NewGuid(), // Mismatched pharmacy!
                ExpectedChainId = _chainId,
                AllowCrossPharmacyMigration = false
            };

            var preview = await _restoreCoordinator.PreflightCheckAsync(request);

            Assert.False(preview.IsValid);
            Assert.False(preview.PharmacyMatch);
            Assert.Contains(preview.Errors, e => e.Contains("Pharmacy ownership mismatch"));
        }

        [Fact]
        public async Task PreflightCheck_CrossPharmacyMigrationAllowed_PassesWithWarning()
        {
            string archivePath = await CreateValidBackupArchiveAsync();

            var request = new RestoreRequest
            {
                ArchiveFilePath = archivePath,
                TargetDatabasePath = _liveDbPath,
                BackupKeySecret = _secret,
                ExpectedPharmacyId = Guid.NewGuid(), // Mismatched pharmacy
                ExpectedChainId = _chainId,
                AllowCrossPharmacyMigration = true // Allowed explicitly!
            };

            var preview = await _restoreCoordinator.PreflightCheckAsync(request);

            Assert.True(preview.IsValid);
            Assert.True(preview.PharmacyMatch);
            Assert.Contains(preview.SafetyWarnings, w => w.Contains("Cross-pharmacy migration enabled"));
        }

        [Fact]
        public async Task ExecuteRestoreAsync_DryRun_DoesNotModifyTargetDatabase()
        {
            string archivePath = await CreateValidBackupArchiveAsync();

            // Mutate live database
            File.WriteAllText(_liveDbPath, "MODIFIED_LIVE_DATA_DURING_DAY");

            var request = new RestoreRequest
            {
                ArchiveFilePath = archivePath,
                TargetDatabasePath = _liveDbPath,
                BackupKeySecret = _secret,
                ExpectedPharmacyId = _pharmacyId,
                ExpectedChainId = _chainId,
                DryRunOnly = true
            };

            var result = await _restoreCoordinator.ExecuteRestoreAsync(request, _authorizedAdmin);

            Assert.True(result.Success);
            Assert.Equal(RestoreStep.Preflight, result.Data!.FinalStep);

            // Live database must remain unmodified
            string currentContent = File.ReadAllText(_liveDbPath);
            Assert.Equal("MODIFIED_LIVE_DATA_DURING_DAY", currentContent);
        }

        [Fact]
        public async Task ExecuteRestoreAsync_FullRestore_RestoresDatabaseAndRotatesGeneration()
        {
            string archivePath = await CreateValidBackupArchiveAsync();

            // Mutate live database before restore
            File.WriteAllText(_liveDbPath, "CORRUPTED_OR_MUTATED_STATE");

            var request = new RestoreRequest
            {
                ArchiveFilePath = archivePath,
                TargetDatabasePath = _liveDbPath,
                BackupKeySecret = _secret,
                ExpectedPharmacyId = _pharmacyId,
                ExpectedChainId = _chainId
            };

            var result = await _restoreCoordinator.ExecuteRestoreAsync(request, _authorizedAdmin);

            Assert.True(result.Success);
            Assert.Equal(RestoreStep.Completed, result.Data!.FinalStep);

            // Verify live database has reverted to backup content
            string restoredContent = File.ReadAllText(_liveDbPath);
            Assert.Equal("INITIAL_DATABASE_STATE_ORIGINAL_REVISION_1", restoredContent);

            // Verify safety copy was created
            string? safetyCopyPath = result.Data.PreRestoreSafetyCopyPath;
            Assert.False(string.IsNullOrWhiteSpace(safetyCopyPath));
            Assert.True(File.Exists(safetyCopyPath));
            Assert.Equal("CORRUPTED_OR_MUTATED_STATE", File.ReadAllText(safetyCopyPath));

            // Verify reconciliation & recovery generation rotated
            Assert.NotNull(result.Data.Reconciliation);
            Assert.NotEqual(Guid.Empty, result.Data.Reconciliation.NewRecoveryGeneration);
            Assert.False(_activationService.IsMaintenanceModeActive);
        }

        [Fact]
        public async Task ExecuteRestoreAsync_UnauthorizedUser_Rejected()
        {
            string archivePath = await CreateValidBackupArchiveAsync();

            var unauthorizedUser = new UserIdentity
            {
                UserId = Guid.NewGuid(),
                Username = "regular_operator",
                Permissions = new List<string> { Permissions.BackupCreate } // Lacks RestoreExecute
            };

            var request = new RestoreRequest
            {
                ArchiveFilePath = archivePath,
                TargetDatabasePath = _liveDbPath,
                BackupKeySecret = _secret,
                ExpectedPharmacyId = _pharmacyId
            };

            var result = await _restoreCoordinator.ExecuteRestoreAsync(request, unauthorizedUser);

            Assert.False(result.Success);
            Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        }

        [Fact]
        public async Task ExecuteRestoreAsync_ReconciliationFails_PerformsRollback()
        {
            string archivePath = await CreateValidBackupArchiveAsync();

            File.WriteAllText(_liveDbPath, "PRE_RESTORE_IMPORTANT_DATA");

            // Setup a real coordinator where transport throws during cloud checkpoint query
            var failingTransport = Substitute.For<ISyncTransport>();
            failingTransport.GetCloudCheckpointAsync(Arg.Any<Guid>(), Arg.Any<System.Threading.CancellationToken>())
                .Returns<Task<long>>(_ => throw new InvalidOperationException("Simulated cloud reconciliation failure!"));

            var failingSync = new PostRestoreSyncCoordinator(
                new SyncCheckpointCoordinator(),
                new InMemorySyncStore(),
                new InMemorySyncStore(),
                failingTransport,
                new SyncConflictHandler(),
                NullLogger<PostRestoreSyncCoordinator>.Instance);

            var coordinatorWithFailure = new RestoreCoordinator(
                _preflight,
                _stagingService,
                _activationService,
                failingSync,
                NullLogger<RestoreCoordinator>.Instance);

            var request = new RestoreRequest
            {
                ArchiveFilePath = archivePath,
                TargetDatabasePath = _liveDbPath,
                BackupKeySecret = _secret,
                ExpectedPharmacyId = _pharmacyId,
                ExpectedChainId = _chainId
            };

            var result = await coordinatorWithFailure.ExecuteRestoreAsync(request, _authorizedAdmin);

            Assert.False(result.Success);
            Assert.Equal("RESTORE_FAILED", result.ErrorCode);

            // Verify live DB was rolled back from safety copy
            string currentDb = File.ReadAllText(_liveDbPath);
            Assert.Equal("PRE_RESTORE_IMPORTANT_DATA", currentDb);
            Assert.False(_activationService.IsMaintenanceModeActive);
        }
    }
}
