using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Restore;
using Sops.DataOperations.Sync;

namespace Sops.DataOperations.Diagnostics
{
    /// <summary>
    /// Consolidated operations health and real-time progress event publisher.
    /// </summary>
    public sealed class OperationsHealthProvider : IOperationsStatusProvider
    {
        private readonly SyncStatusProvider _syncStatus;
        private readonly RestoreActivationService _restoreActivation;
        private DateTimeOffset? _lastSuccessfulBackupUtc;
        private bool _lastBackupVerified;
        private DateTimeOffset? _lastRestoreUtc;
        private string? _recoveryWarning;
        private readonly object _lock = new object();

        public event EventHandler<OperationProgressReport>? ProgressChanged;

        public OperationsHealthProvider(
            SyncStatusProvider syncStatus,
            RestoreActivationService restoreActivation)
        {
            _syncStatus = syncStatus ?? throw new ArgumentNullException(nameof(syncStatus));
            _restoreActivation = restoreActivation ?? throw new ArgumentNullException(nameof(restoreActivation));
        }

        public void ReportProgress(OperationProgressReport report)
        {
            if (report == null) return;
            ProgressChanged?.Invoke(this, report);
        }

        public void RecordBackupOutcome(bool success, bool verified)
        {
            lock (_lock)
            {
                if (success)
                {
                    _lastSuccessfulBackupUtc = DateTimeOffset.UtcNow;
                    _lastBackupVerified = verified;
                }
            }
        }

        public void RecordRestoreOutcome(bool success, string? warning = null)
        {
            lock (_lock)
            {
                if (success)
                {
                    _lastRestoreUtc = DateTimeOffset.UtcNow;
                }
                _recoveryWarning = warning;
            }
        }

        public async Task<OperationsHealthSummary> GetHealthSummaryAsync(CancellationToken ct = default)
        {
            var sync = await _syncStatus.GetStatusAsync(ct);

            long freeSpace = 0;
            try
            {
                var drive = new DriveInfo("C:\\");
                freeSpace = drive.AvailableFreeSpace;
            }
            catch { }

            lock (_lock)
            {
                bool isHealthy = sync.State != SyncState.Error &&
                                 sync.State != SyncState.RecoveryRequired &&
                                 !_restoreActivation.IsMaintenanceModeActive &&
                                 freeSpace > 500 * 1024 * 1024;

                return new OperationsHealthSummary
                {
                    IsHealthy = isHealthy,
                    SyncStatus = sync,
                    PendingApprovalRequestsCount = 0,
                    LastSuccessfulBackupUtc = _lastSuccessfulBackupUtc,
                    LastBackupVerified = _lastBackupVerified,
                    LastRestoreUtc = _lastRestoreUtc,
                    ActiveMaintenanceMode = _restoreActivation.IsMaintenanceModeActive,
                    FreeDiskSpaceBytes = freeSpace,
                    OutstandingRecoveryWarning = _recoveryWarning
                };
            }
        }
    }
}
