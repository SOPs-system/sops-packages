using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Backup;

namespace Sops.DataOperations.Restore
{
    /// <summary>
    /// Executes preflight validation to determine restore eligibility without touching live data.
    /// </summary>
    public sealed class RestorePreflight
    {
        private readonly BackupVerificationService _verifier;
        private readonly ILogger<RestorePreflight>? _logger;

        public RestorePreflight(BackupVerificationService verifier, ILogger<RestorePreflight>? logger = null)
        {
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _logger = logger;
        }

        public async Task<RestorePreview> CheckAsync(RestoreRequest request, CancellationToken ct = default)
        {
            var preview = new RestorePreview();

            if (request == null)
            {
                preview.Errors.Add("Restore request cannot be null.");
                return preview;
            }

            if (!File.Exists(request.ArchiveFilePath))
            {
                preview.Errors.Add($"Backup archive file not found: {request.ArchiveFilePath}");
                return preview;
            }

            // 1. Verify Archive Integrity & Manifest
            var verifyResult = await _verifier.VerifyArchiveAsync(request.ArchiveFilePath, request.BackupKeySecret, ct);
            if (!verifyResult.IsValid)
            {
                preview.Errors.Add($"Backup archive verification failed: {verifyResult.ErrorMessage}");
                return preview;
            }

            preview.Manifest = verifyResult.Manifest;

            // 2. Validate Pharmacy Ownership
            if (preview.Manifest != null)
            {
                if (request.ExpectedPharmacyId != Guid.Empty && preview.Manifest.PharmacyId != request.ExpectedPharmacyId)
                {
                    if (!request.AllowCrossPharmacyMigration)
                    {
                        preview.Errors.Add($"Pharmacy ownership mismatch: Archive belongs to pharmacy '{preview.Manifest.PharmacyId}', expected '{request.ExpectedPharmacyId}'.");
                        preview.PharmacyMatch = false;
                    }
                    else
                    {
                        preview.SafetyWarnings.Add($"Cross-pharmacy migration enabled: Overwriting pharmacy '{request.ExpectedPharmacyId}' with data from '{preview.Manifest.PharmacyId}'.");
                        preview.PharmacyMatch = true;
                    }
                }
                else
                {
                    preview.PharmacyMatch = true;
                }

                // 3. Validate Chain Scope
                if (request.ExpectedChainId.HasValue && preview.Manifest.ChainId.HasValue && request.ExpectedChainId != preview.Manifest.ChainId)
                {
                    preview.Errors.Add($"Chain scope mismatch: Archive belongs to chain '{preview.Manifest.ChainId}', expected '{request.ExpectedChainId}'.");
                    preview.TenantMatch = false;
                }
                else
                {
                    preview.TenantMatch = true;
                }

                // 4. Schema Compatibility
                preview.SchemaCompatible = !string.IsNullOrWhiteSpace(preview.Manifest.DatabaseSchemaVersion);
                if (!preview.SchemaCompatible)
                {
                    preview.Errors.Add("Database schema version is missing from archive manifest.");
                }

                // 5. Disk Space Check
                long requiredBytes = (preview.Manifest.SourceDatabaseSizeBytes > 0 ? preview.Manifest.SourceDatabaseSizeBytes : preview.Manifest.ArchiveSizeBytes) * 3; // Staging + Live + Safety copy
                preview.RequiredDiskSpaceBytes = requiredBytes;

                string targetRoot = Path.GetPathRoot(Path.GetFullPath(request.TargetDatabasePath)) ?? "C:\\";
                DriveInfo drive = new DriveInfo(targetRoot);
                preview.AvailableDiskSpaceBytes = drive.AvailableFreeSpace;

                if (drive.AvailableFreeSpace < requiredBytes)
                {
                    preview.Errors.Add($"Insufficient disk space on '{drive.Name}'. Required: {requiredBytes} bytes, Available: {drive.AvailableFreeSpace} bytes.");
                }

                // 6. Safety Warnings
                if (File.Exists(request.TargetDatabasePath))
                {
                    preview.SafetyWarnings.Add("Existing live database will be backed up to a recoverable safety copy before activation.");
                }
            }

            preview.IsValid = preview.Errors.Count == 0;
            preview.DryRunSuccessful = preview.IsValid;
            return preview;
        }
    }
}
