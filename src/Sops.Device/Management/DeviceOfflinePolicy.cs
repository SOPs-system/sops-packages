using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Comprehensive offline classification of a device evaluating local credentials,
    /// authoritative server status, clock tampering, and offline grace windows.
    /// </summary>
    public enum DeviceOfflineState
    {
        /// <summary>Device identity has been initialized locally but never registered with the authoritative server.</summary>
        NeverRegistered = 0,

        /// <summary>Device is registered, currently online, and synchronized with the authoritative server.</summary>
        ActiveOnline = 1,

        /// <summary>Device is disconnected from the server but operating within its permitted offline grace window.</summary>
        WithinGracePeriod = 2,

        /// <summary>Backward-compatible alias for <see cref="WithinGracePeriod"/>.</summary>
        ActiveOfflineGrace = 2,

        /// <summary>Device offline grace period has expired; reconnection and re-authentication are required.</summary>
        OfflineGraceExpired = 3,

        /// <summary>Device registration is temporarily suspended by administrative order.</summary>
        Suspended = 4,

        /// <summary>Device registration has been permanently revoked. Privileged operations are terminated.</summary>
        Revoked = 5,

        /// <summary>Stored credentials are missing, corrupted, or failed decryption.</summary>
        CredentialsInvalid = 6,

        /// <summary>Physical hardware drift exceeds permitted tolerance; device re-verification is required.</summary>
        HardwareDriftExceeded = 7,

        /// <summary>Significant clock rollback detected; current system clock is prior to last sync minus tolerance.</summary>
        ClockRollbackDetected = 8
    }

    /// <summary>
    /// Configurable policy governing offline grace behavior and clock rollback tolerance.
    /// Enables pharmacy organizations and higher-level licensing to customize or disable offline grace windows.
    /// </summary>
    public sealed class DeviceOfflinePolicy
    {
        /// <summary>Default configurable offline grace duration (14 days).</summary>
        public static readonly TimeSpan StandardGracePeriod = TimeSpan.FromDays(14);

        /// <summary>Configurable offline grace duration allowed before server reconnection is mandatory.</summary>
        public TimeSpan GracePeriod { get; set; } = StandardGracePeriod;

        /// <summary>Maximum tolerated system clock rollback before detecting potential clock tampering.</summary>
        public TimeSpan MaxClockRollbackTolerance { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Whether offline operation is permitted for this pharmacy workstation tier.</summary>
        public bool AllowOfflineOperations { get; set; } = true;

        /// <summary>Policy requiring offline operation to strictly adhere to server-signed validity windows.</summary>
        public bool RequireSignedValidityWindow { get; set; } = false;

        public static DeviceOfflinePolicy StrictNoOffline() =>
            new DeviceOfflinePolicy { AllowOfflineOperations = false, GracePeriod = TimeSpan.Zero };

        public static DeviceOfflinePolicy Custom(TimeSpan gracePeriod) =>
            new DeviceOfflinePolicy { AllowOfflineOperations = gracePeriod > TimeSpan.Zero, GracePeriod = gracePeriod };
    }
}
