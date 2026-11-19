using System;
using System.Collections.Generic;

namespace Sops.DataOperations.Abstractions.Models
{
    /// <summary>
    /// Parameters to initiate a database backup.
    /// </summary>
    public sealed class BackupRequest
    {
        public string SourceDatabasePath { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = string.Empty;
        public string? SqlCipherPassword { get; set; }
        public string? BackupKeySecret { get; set; }
        public string? KeyIdentifier { get; set; } = "primary";
        public Guid PharmacyId { get; set; }
        public Guid? ChainId { get; set; }
        public string InstallationId { get; set; } = string.Empty;
        public string? CustomFileName { get; set; }
        public string ApplicationVersion { get; set; } = "1.0.0";
        public string DatabaseSchemaVersion { get; set; } = "1.0.0";
        public long CurrentCheckpoint { get; set; }
        public Guid CurrentRecoveryGeneration { get; set; } = Guid.NewGuid();
    }

    /// <summary>
    /// Outcome of a database backup creation operation.
    /// </summary>
    public sealed class BackupResult
    {
        public bool Success { get; set; }
        public string ArchivePath { get; set; } = string.Empty;
        public BackupManifest? Manifest { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of backup verification.
    /// </summary>
    public sealed class BackupVerificationResult
    {
        public bool IsValid { get; set; }
        public BackupManifest? Manifest { get; set; }
        public bool MacVerified { get; set; }
        public bool ChecksumVerified { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTimeOffset VerifiedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Retention policy for automated backup cleanup.
    /// </summary>
    public sealed class BackupRetentionPolicy
    {
        public int MaxBackupsToKeep { get; set; } = 30;
        public int RetainDays { get; set; } = 60;
        public bool AlwaysKeepAtLeastOne { get; set; } = true;
    }

    /// <summary>
    /// Parameters for a database restoration operation.
    /// </summary>
    public sealed class RestoreRequest
    {
        public string ArchiveFilePath { get; set; } = string.Empty;
        public string TargetDatabasePath { get; set; } = string.Empty;
        public string StagingDirectory { get; set; } = string.Empty;
        public string? BackupKeySecret { get; set; }
        public string? ExpectedSqlCipherPassword { get; set; }
        public Guid ExpectedPharmacyId { get; set; }
        public Guid? ExpectedChainId { get; set; }
        public bool AllowCrossPharmacyMigration { get; set; }
        public bool DryRunOnly { get; set; }
    }

    /// <summary>
    /// Execution result of a database restoration.
    /// </summary>
    public sealed class RestoreExecutionResult
    {
        public bool Success { get; set; }
        public RestoreStep FinalStep { get; set; }
        public BackupManifest? Manifest { get; set; }
        public string? PreRestoreSafetyCopyPath { get; set; }
        public ReconciliationResult? Reconciliation { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RolledBack { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Restore execution steps.
    /// </summary>
    public enum RestoreStep
    {
        Requested,
        Preflight,
        Authenticated,
        Staging,
        IntegrityVerified,
        MaintenanceMode,
        SafetyCopyCreated,
        Activated,
        ReconciliationPending,
        Completed,
        RolledBack,
        Failed
    }
}
