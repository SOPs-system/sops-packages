using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Aggregated operations health snapshot.
    /// </summary>
    public sealed class OperationsHealthSummary
    {
        public bool IsHealthy { get; set; } = true;
        public SyncStatusInfo SyncStatus { get; set; } = new SyncStatusInfo();
        public int PendingApprovalRequestsCount { get; set; }
        public DateTimeOffset? LastSuccessfulBackupUtc { get; set; }
        public bool LastBackupVerified { get; set; }
        public DateTimeOffset? LastRestoreUtc { get; set; }
        public bool ActiveMaintenanceMode { get; set; }
        public long FreeDiskSpaceBytes { get; set; }
        public string? OutstandingRecoveryWarning { get; set; }
    }

    /// <summary>
    /// Provides real-time progress events and consolidated health status.
    /// </summary>
    public interface IOperationsStatusProvider
    {
        /// <summary>Gets the consolidated operations health summary.</summary>
        Task<OperationsHealthSummary> GetHealthSummaryAsync(CancellationToken ct = default);

        /// <summary>Occurs when an operational progress milestone is reached.</summary>
        event EventHandler<OperationProgressReport>? ProgressChanged;

        /// <summary>Notifies subscribers of an updated progress report.</summary>
        void ReportProgress(OperationProgressReport report);
    }
}
