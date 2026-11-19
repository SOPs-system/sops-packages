using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Backup
{
    /// <summary>
    /// Enforces backup retention policies to prevent disk exhaustion.
    /// </summary>
    public sealed class BackupRetentionManager
    {
        private readonly ILogger<BackupRetentionManager>? _logger;

        public BackupRetentionManager(ILogger<BackupRetentionManager>? logger = null)
        {
            _logger = logger;
        }

        public Task<int> EnforceRetentionAsync(
            string directoryPath,
            BackupRetentionPolicy policy,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return Task.FromResult(0);

            if (policy == null) policy = new BackupRetentionPolicy();

            int deletedCount = 0;
            try
            {
                var files = Directory.GetFiles(directoryPath, "*.sopsbak")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                if (files.Count == 0) return Task.FromResult(0);

                int keepMinimum = policy.AlwaysKeepAtLeastOne ? 1 : 0;
                var cutoffDate = DateTimeOffset.UtcNow.AddDays(-policy.RetainDays);

                for (int i = 0; i < files.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    // Never delete if within keepMinimum quota
                    if (i < keepMinimum) continue;

                    bool exceedsMaxCount = (i >= policy.MaxBackupsToKeep);
                    bool exceedsAge = (files[i].CreationTimeUtc < cutoffDate.UtcDateTime);

                    if (exceedsMaxCount || exceedsAge)
                    {
                        try
                        {
                            files[i].Delete();
                            deletedCount++;
                            _logger?.LogInformation("Purged expired backup archive: {Path}", files[i].FullName);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Could not delete backup file {Path}.", files[i].FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed enforcing retention policy in {Directory}.", directoryPath);
            }

            return Task.FromResult(deletedCount);
        }
    }
}
