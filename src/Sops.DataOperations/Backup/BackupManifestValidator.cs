using System;
using System.Collections.Generic;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Backup
{
    /// <summary>
    /// Validates critical metadata fields of an authenticated backup manifest.
    /// </summary>
    public static class BackupManifestValidator
    {
        public static (bool IsValid, List<string> Errors) Validate(BackupManifest manifest)
        {
            var errors = new List<string>();

            if (manifest == null)
            {
                errors.Add("Backup manifest cannot be null.");
                return (false, errors);
            }

            if (manifest.BackupId == Guid.Empty)
                errors.Add("BackupId is missing or empty.");

            if (manifest.PharmacyId == Guid.Empty)
                errors.Add("PharmacyId is missing or empty.");

            if (manifest.FormatVersion < 1)
                errors.Add($"Unsupported backup format version '{manifest.FormatVersion}'.");

            if (string.IsNullOrWhiteSpace(manifest.EncryptionAlgorithm))
                errors.Add("EncryptionAlgorithm is required.");

            if (string.IsNullOrWhiteSpace(manifest.ChecksumSha256))
                errors.Add("ChecksumSha256 is required.");

            if (string.IsNullOrWhiteSpace(manifest.IntegrityMac))
                errors.Add("IntegrityMac is required.");

            if (manifest.ArchiveSizeBytes <= 0)
                errors.Add("ArchiveSizeBytes must be greater than zero.");

            return (errors.Count == 0, errors);
        }
    }
}
