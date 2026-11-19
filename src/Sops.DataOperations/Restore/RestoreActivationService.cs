using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Sops.DataOperations.Restore
{
    /// <summary>
    /// Executes maintenance-mode locking, pre-restore safety copy creation, atomic database swap, and rollback.
    /// </summary>
    public sealed class RestoreActivationService
    {
        private readonly ILogger<RestoreActivationService>? _logger;
        private readonly object _lock = new object();
        private bool _isMaintenanceModeActive;

        public RestoreActivationService(ILogger<RestoreActivationService>? logger = null)
        {
            _logger = logger;
        }

        public bool IsMaintenanceModeActive => _isMaintenanceModeActive;

        public void EnterMaintenanceMode(string reason)
        {
            lock (_lock)
            {
                _isMaintenanceModeActive = true;
                _logger?.LogWarning("Maintenance mode activated: {Reason}", reason);
            }
        }

        public void ExitMaintenanceMode()
        {
            lock (_lock)
            {
                _isMaintenanceModeActive = false;
                _logger?.LogInformation("Maintenance mode deactivated.");
            }
        }

        /// <summary>
        /// Creates a recoverable pre-restore safety copy of the current live database.
        /// </summary>
        public string CreatePreRestoreSafetyCopy(string liveDatabasePath)
        {
            if (!File.Exists(liveDatabasePath))
                return string.Empty;

            string dir = Path.GetDirectoryName(liveDatabasePath) ?? ".";
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            string safetyCopyPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(liveDatabasePath)}_safety_{timestamp}.live.bak");

            File.Copy(liveDatabasePath, safetyCopyPath, overwrite: true);
            _logger?.LogInformation("Pre-restore safety copy created: {Path}", safetyCopyPath);
            return safetyCopyPath;
        }

        /// <summary>
        /// Atomically activates the staging database into the live database location.
        /// </summary>
        public void ActivateStagedDatabase(string stagingFilePath, string liveDatabasePath, string safetyCopyPath)
        {
            if (!File.Exists(stagingFilePath))
                throw new FileNotFoundException("Staging database file not found.", stagingFilePath);

            try
            {
                // Delete existing live file if safety copy is confirmed
                if (File.Exists(liveDatabasePath))
                {
                    File.Delete(liveDatabasePath);
                }

                // Move staging file into place
                File.Move(stagingFilePath, liveDatabasePath);
                _logger?.LogInformation("Staging database activated successfully at {Path}.", liveDatabasePath);
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Activation swap failed! Initiating emergency rollback from {SafetyCopy}...", safetyCopyPath);
                Rollback(safetyCopyPath, liveDatabasePath);
                throw;
            }
        }

        /// <summary>
        /// Restores the original live database from the safety copy.
        /// </summary>
        public void Rollback(string safetyCopyPath, string liveDatabasePath)
        {
            if (!string.IsNullOrWhiteSpace(safetyCopyPath) && File.Exists(safetyCopyPath))
            {
                try
                {
                    if (File.Exists(liveDatabasePath))
                    {
                        File.Delete(liveDatabasePath);
                    }
                    File.Copy(safetyCopyPath, liveDatabasePath, overwrite: true);
                    _logger?.LogWarning("Emergency rollback succeeded: Live database restored from safety copy {Path}.", safetyCopyPath);
                }
                catch (Exception rollbackEx)
                {
                    _logger?.LogCritical(rollbackEx, "CRITICAL: Rollback failed! Safety copy preserved at {Path}.", safetyCopyPath);
                }
            }
        }
    }
}
