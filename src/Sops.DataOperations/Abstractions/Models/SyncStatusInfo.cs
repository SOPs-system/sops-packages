using System;

namespace Sops.DataOperations.Abstractions.Models
{
    /// <summary>
    /// Lifecycle states for offline-first synchronization.
    /// </summary>
    public enum SyncState
    {
        Offline,
        Idle,
        Connecting,
        Synchronizing,
        WaitingForApproval,
        Conflict,
        RetryScheduled,
        Paused,
        Error,
        RecoveryRequired
    }

    /// <summary>
    /// Scope of synchronization operation.
    /// </summary>
    public enum SyncScope
    {
        All,
        GlobalOnly,
        TenantOnly,
        OutboxOnly,
        InboxOnly
    }

    /// <summary>
    /// Comprehensive synchronization status details.
    /// </summary>
    public sealed class SyncStatusInfo
    {
        public SyncState State { get; set; } = SyncState.Idle;
        public DateTimeOffset? LastSuccessfulSyncUtc { get; set; }
        public int PendingOutboxCount { get; set; }
        public int PendingInboxCount { get; set; }
        public int RetryCount { get; set; }
        public int FailedEventCount { get; set; }
        public string? LastErrorSummary { get; set; }
        public bool GlobalConnectivity { get; set; }
        public bool TenantConnectivity { get; set; }
        public Guid CurrentRecoveryGeneration { get; set; } = Guid.NewGuid();
        public long LastCheckpoint { get; set; }
        public string? PauseReason { get; set; }
        public DateTimeOffset LastStatusCheckUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Outcome of a sync batch or trigger execution.
    /// </summary>
    public sealed class SyncResult
    {
        public bool Success { get; set; }
        public int EventsUploaded { get; set; }
        public int EventsDownloaded { get; set; }
        public int ConflictsDetected { get; set; }
        public long NewCheckpoint { get; set; }
        public string? Message { get; set; }
        public bool Retried { get; set; }
    }
}
