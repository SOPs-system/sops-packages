using System;

namespace Sops.DataOperations.Backup
{
    /// <summary>
    /// Configuration options for automated database backup scheduling and retention.
    /// </summary>
    public sealed class BackupPolicy
    {
        public bool ScheduledBackupsEnabled { get; set; } = true;
        public TimeSpan BackupInterval { get; set; } = TimeSpan.FromHours(24);
        public string DefaultOutputDirectory { get; set; } = string.Empty;
        public int MaxRetainedBackups { get; set; } = 30;
        public int RetainDays { get; set; } = 60;
        public bool RequireVerificationOnCompletion { get; set; } = true;
        public long MinimumFreeDiskSpaceBytes { get; set; } = 500 * 1024 * 1024; // 500 MB safety buffer
    }
}
