using System;
using System.Collections.Generic;

namespace Sops.DataOperations.Abstractions.Models
{
    /// <summary>
    /// Authenticated preflight analysis before database restoration.
    /// </summary>
    public sealed class RestorePreview
    {
        public bool IsValid { get; set; }
        public BackupManifest? Manifest { get; set; }
        public bool PharmacyMatch { get; set; }
        public bool TenantMatch { get; set; }
        public bool SchemaCompatible { get; set; }
        public long RequiredDiskSpaceBytes { get; set; }
        public long AvailableDiskSpaceBytes { get; set; }
        public bool ActiveTransactionsDetected { get; set; }
        public List<string> SafetyWarnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public bool DryRunSuccessful { get; set; }
    }

    /// <summary>
    /// Progress event for long-running backup, restore, or sync operations.
    /// </summary>
    public sealed class OperationProgressReport
    {
        public Guid OperationId { get; set; }
        public string OperationName { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public string? Details { get; set; }
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// User identity for authorization and separation of duties checks.
    /// </summary>
    public sealed class UserIdentity
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public Guid PharmacyId { get; set; }
        public Guid? ChainId { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public List<string> Permissions { get; set; } = new List<string>();

        public bool HasPermission(string permission)
        {
            return Permissions != null && Permissions.Contains(permission);
        }

        public bool IsInRole(string role)
        {
            return Roles != null && Roles.Contains(role);
        }
    }

    /// <summary>
    /// Post-restore cloud synchronization reconciliation analysis and execution result.
    /// </summary>
    public sealed class ReconciliationResult
    {
        public Guid PharmacyId { get; set; }
        public Guid PreviousRecoveryGeneration { get; set; }
        public Guid NewRecoveryGeneration { get; set; }
        public long RestoredCheckpoint { get; set; }
        public long CloudCheckpoint { get; set; }
        public int DuplicateEventsCount { get; set; }
        public int MissingCloudEventsRecovered { get; set; }
        public int LocalOutboxOrphanedCount { get; set; }
        public int QuarantinedConflictsCount { get; set; }
        public ReconciliationStatus Status { get; set; }
        public string Details { get; set; } = string.Empty;
        public DateTimeOffset ReconciledAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    public enum ReconciliationStatus
    {
        ReconciledClean,
        ReconciledWithQuarantine,
        ManualReviewRequired,
        Failed
    }
}
