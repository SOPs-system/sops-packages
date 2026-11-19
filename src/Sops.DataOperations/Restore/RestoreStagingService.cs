using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Security;

namespace Sops.DataOperations.Restore
{
    /// <summary>
    /// Staging service that decrypts and verifies the archive in an isolated temporary location before touching live state.
    /// </summary>
    public sealed class RestoreStagingService
    {
        private readonly ILogger<RestoreStagingService>? _logger;

        public RestoreStagingService(ILogger<RestoreStagingService>? logger = null)
        {
            _logger = logger;
        }

        public async Task<string> StageDecryptedDatabaseAsync(
            RestoreRequest request,
            BackupManifest manifest,
            IProgress<OperationProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            string stagingDir = !string.IsNullOrWhiteSpace(request.StagingDirectory)
                ? request.StagingDirectory
                : Path.Combine(Path.GetDirectoryName(request.TargetDatabasePath) ?? ".", ".sops_restore_staging");

            if (!Directory.Exists(stagingDir))
            {
                Directory.CreateDirectory(stagingDir);
            }

            string stagingFilePath = Path.Combine(stagingDir, $"staging_{manifest.BackupId:N}.db");
            if (File.Exists(stagingFilePath))
            {
                File.Delete(stagingFilePath);
            }

            try
            {
                var keys = BackupKeyProvider.DeriveKeys(request.BackupKeySecret ?? string.Empty, manifest.KeyIdentifier);

                using (var archiveStream = new FileStream(request.ArchiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                using (var stagingStream = new FileStream(stagingFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    await EncryptedStreamProcessor.DecryptStreamAsync(archiveStream, stagingStream, keys, progress, ct);
                }

                // Verify decrypted file checksum against manifest
                using (var sha = SHA256.Create())
                using (var verifyStream = new FileStream(stagingFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                {
                    byte[] hash = await Task.Run(() => sha.ComputeHash(verifyStream), ct);
                    string actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    if (!string.Equals(actualChecksum, manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Decrypted database checksum mismatch! Expected: {manifest.ChecksumSha256}, Actual: {actualChecksum}");
                    }
                }

                _logger?.LogInformation("Backup {BackupId} successfully decrypted and checksum-verified in staging: {Path}", manifest.BackupId, stagingFilePath);
                return stagingFilePath;
            }
            catch (Exception ex)
            {
                if (File.Exists(stagingFilePath))
                {
                    try { File.Delete(stagingFilePath); } catch { }
                }

                _logger?.LogError(ex, "Staging failed for backup {BackupId}.", manifest.BackupId);
                throw;
            }
        }
    }
}
