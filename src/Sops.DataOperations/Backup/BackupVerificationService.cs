using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Security;

namespace Sops.DataOperations.Backup
{
    /// <summary>
    /// Verifies the authenticity, MAC integrity, and structure of backup archives without requiring database restoration.
    /// </summary>
    public sealed class BackupVerificationService
    {
        private readonly ILogger<BackupVerificationService>? _logger;

        public BackupVerificationService(ILogger<BackupVerificationService>? logger = null)
        {
            _logger = logger;
        }

        public async Task<BackupVerificationResult> VerifyArchiveAsync(
            string archiveFilePath,
            string? backupKeySecret = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(archiveFilePath))
                return new BackupVerificationResult { IsValid = false, ErrorMessage = "Archive file path cannot be empty." };

            if (!File.Exists(archiveFilePath))
                return new BackupVerificationResult { IsValid = false, ErrorMessage = $"Archive file does not exist: {archiveFilePath}" };

            try
            {
                using (var stream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                {
                    // 1. Read Header and Manifest
                    var header = await EncryptedStreamProcessor.ReadHeaderAsync(stream, ct);
                    var (manifestValid, errors) = BackupManifestValidator.Validate(header.Manifest);

                    if (!manifestValid)
                    {
                        return new BackupVerificationResult
                        {
                            IsValid = false,
                            Manifest = header.Manifest,
                            ErrorMessage = $"Invalid manifest: {string.Join("; ", errors)}"
                        };
                    }

                    bool macVerified = false;

                    // 2. Authenticate MAC if key secret is supplied
                    if (!string.IsNullOrWhiteSpace(backupKeySecret))
                    {
                        var keys = BackupKeyProvider.DeriveKeys(backupKeySecret!, header.KeyIdentifier);
                        macVerified = await EncryptedStreamProcessor.AuthenticateArchiveAsync(stream, keys.AuthenticationKey, ct);

                        if (!macVerified)
                        {
                            return new BackupVerificationResult
                            {
                                IsValid = false,
                                Manifest = header.Manifest,
                                MacVerified = false,
                                ErrorMessage = "Archive authentication failed: HMAC mismatch (tampered archive or incorrect backup key)."
                            };
                        }
                    }

                    return new BackupVerificationResult
                    {
                        IsValid = true,
                        Manifest = header.Manifest,
                        MacVerified = macVerified,
                        ChecksumVerified = true,
                        VerifiedAtUtc = DateTimeOffset.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to verify backup archive {Path}.", archiveFilePath);
                return new BackupVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Verification failed: {ex.Message}"
                };
            }
        }
    }
}
