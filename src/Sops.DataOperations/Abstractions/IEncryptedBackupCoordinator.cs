using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Coordinates stream-based, authenticated AES-256-CBC + HMAC-SHA256 encrypted backups of SQLCipher databases.
    /// </summary>
    public interface IEncryptedBackupCoordinator
    {
        /// <summary>
        /// Creates a consistent, authenticated encrypted backup archive using bounded-memory streaming.
        /// </summary>
        Task<DataOperationResult<BackupResult>> CreateBackupAsync(BackupRequest request, IProgress<OperationProgressReport>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Verifies the cryptographic integrity, authentication MAC, and checksum of a backup archive without writing plaintext to disk.
        /// </summary>
        Task<BackupVerificationResult> VerifyBackupAsync(string archiveFilePath, string? backupKey = null, CancellationToken ct = default);

        /// <summary>
        /// Inspects and lists all available backup manifests in a directory.
        /// </summary>
        Task<IReadOnlyList<BackupManifest>> GetBackupHistoryAsync(string directoryPath, CancellationToken ct = default);

        /// <summary>
        /// Cleans up older backup archives according to retention policy rules.
        /// </summary>
        Task<int> EnforceRetentionPolicyAsync(string directoryPath, BackupRetentionPolicy policy, CancellationToken ct = default);
    }
}
