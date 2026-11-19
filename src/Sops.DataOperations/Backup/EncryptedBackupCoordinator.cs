using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Security;

namespace Sops.DataOperations.Backup
{
    /// <summary>
    /// Coordinates stream-based, authenticated encrypted backups of local SQLCipher databases.
    /// </summary>
    public sealed class EncryptedBackupCoordinator : IEncryptedBackupCoordinator
    {
        private readonly BackupVerificationService _verifier;
        private readonly BackupRetentionManager _retentionManager;
        private readonly BackupPolicy _policy;
        private readonly ILogger<EncryptedBackupCoordinator>? _logger;

        public EncryptedBackupCoordinator(
            BackupVerificationService verifier,
            BackupRetentionManager retentionManager,
            BackupPolicy? policy = null,
            ILogger<EncryptedBackupCoordinator>? logger = null)
        {
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _retentionManager = retentionManager ?? throw new ArgumentNullException(nameof(retentionManager));
            _policy = policy ?? new BackupPolicy();
            _logger = logger;
        }

        public async Task<DataOperationResult<BackupResult>> CreateBackupAsync(
            BackupRequest request,
            IProgress<OperationProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.SourceDatabasePath) || !File.Exists(request.SourceDatabasePath))
            {
                return DataOperationResult<BackupResult>.Failed("SOURCE_NOT_FOUND", $"Source database not found: {request.SourceDatabasePath}");
            }

            string outputDir = !string.IsNullOrWhiteSpace(request.OutputDirectory)
                ? request.OutputDirectory
                : (!string.IsNullOrWhiteSpace(_policy.DefaultOutputDirectory) ? _policy.DefaultOutputDirectory : Path.GetDirectoryName(request.SourceDatabasePath) ?? ".");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var stopwatch = Stopwatch.StartNew();
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName = !string.IsNullOrWhiteSpace(request.CustomFileName)
                ? request.CustomFileName!
                : $"sops_backup_{request.PharmacyId:N}_{timestamp}.sopsbak";

            string finalArchivePath = Path.Combine(outputDir, fileName);
            string tempArchivePath = finalArchivePath + ".tmp";

            try
            {
                // 1. Check disk space safety buffer
                var sourceInfo = new FileInfo(request.SourceDatabasePath);
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(outputDir)) ?? "C:\\");
                if (drive.AvailableFreeSpace < (sourceInfo.Length + _policy.MinimumFreeDiskSpaceBytes))
                {
                    return DataOperationResult<BackupResult>.Failed("INSUFFICIENT_DISK_SPACE", $"Insufficient free disk space on drive '{drive.Name}'. Required: {sourceInfo.Length} bytes + {_policy.MinimumFreeDiskSpaceBytes} safety buffer.");
                }

                // 2. Derive Encryption & Authentication Key Material
                string secret = !string.IsNullOrWhiteSpace(request.BackupKeySecret)
                    ? request.BackupKeySecret!
                    : BackupKeyProvider.GenerateRandomMasterSecret();

                var keys = BackupKeyProvider.DeriveKeys(secret, request.KeyIdentifier ?? "primary");

                // 3. Build Manifest
                var manifest = new BackupManifest
                {
                    BackupId = Guid.NewGuid(),
                    FormatVersion = 1,
                    PharmacyId = request.PharmacyId,
                    ChainId = request.ChainId,
                    InstallationId = request.InstallationId,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    DatabaseSchemaVersion = request.DatabaseSchemaVersion,
                    ApplicationVersion = request.ApplicationVersion,
                    RecoveryGeneration = request.CurrentRecoveryGeneration,
                    SnapshotCheckpoint = request.CurrentCheckpoint,
                    SourceDatabaseSizeBytes = sourceInfo.Length,
                    EncryptionAlgorithm = "AES-256-CBC+HMAC-SHA256",
                    KeyIdentifier = keys.KeyIdentifier,
                    IsSqlCipher = true
                };

                // 4. Stream Snapshot into Authenticated Ciphertext Archive
                using (var sourceStream = new FileStream(request.SourceDatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, useAsync: true))
                using (var destStream = new FileStream(tempArchivePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    manifest = await EncryptedStreamProcessor.EncryptStreamAsync(sourceStream, destStream, keys, manifest, progress, ct);
                }

                // 5. Atomic Rename
                if (File.Exists(finalArchivePath))
                {
                    File.Delete(finalArchivePath);
                }
                File.Move(tempArchivePath, finalArchivePath);

                // 6. Immediate Verification Gate
                if (_policy.RequireVerificationOnCompletion)
                {
                    var verifyResult = await _verifier.VerifyArchiveAsync(finalArchivePath, secret, ct);
                    if (!verifyResult.IsValid || !verifyResult.MacVerified)
                    {
                        File.Delete(finalArchivePath);
                        return DataOperationResult<BackupResult>.Failed("VERIFICATION_FAILED", $"Backup archive post-creation verification failed: {verifyResult.ErrorMessage}");
                    }
                }

                stopwatch.Stop();
                _logger?.LogInformation("Backup {BackupId} created successfully at {Path} in {Elapsed}.", manifest.BackupId, finalArchivePath, stopwatch.Elapsed);

                var backupResult = new BackupResult
                {
                    Success = true,
                    ArchivePath = finalArchivePath,
                    Manifest = manifest,
                    Duration = stopwatch.Elapsed
                };

                return DataOperationResult<BackupResult>.Succeeded(backupResult);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempArchivePath))
                {
                    try { File.Delete(tempArchivePath); } catch { }
                }

                _logger?.LogError(ex, "Failed to create encrypted database backup for pharmacy {PharmacyId}.", request.PharmacyId);
                return DataOperationResult<BackupResult>.Failed("BACKUP_FAILED", ex.Message);
            }
        }

        public Task<BackupVerificationResult> VerifyBackupAsync(string archiveFilePath, string? backupKey = null, CancellationToken ct = default)
        {
            return _verifier.VerifyArchiveAsync(archiveFilePath, backupKey, ct);
        }

        public async Task<IReadOnlyList<BackupManifest>> GetBackupHistoryAsync(string directoryPath, CancellationToken ct = default)
        {
            var manifests = new List<BackupManifest>();
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return manifests;

            var files = Directory.GetFiles(directoryPath, "*.sopsbak");
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    {
                        var header = await EncryptedStreamProcessor.ReadHeaderAsync(stream, ct);
                        manifests.Add(header.Manifest);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Skipping invalid archive {Path} during history scan.", file);
                }
            }

            return manifests.OrderByDescending(m => m.CreatedAtUtc).ToList();
        }

        public Task<int> EnforceRetentionPolicyAsync(string directoryPath, BackupRetentionPolicy policy, CancellationToken ct = default)
        {
            return _retentionManager.EnforceRetentionAsync(directoryPath, policy, ct);
        }
    }
}
