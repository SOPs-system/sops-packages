using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Response returned by the authoritative device registry upon processing a periodic heartbeat.
    /// Delivers current server status and instructions (e.g. required configuration sync or revocation order).
    /// </summary>
    public sealed class DeviceHeartbeatResult
    {
        /// <summary>Whether the heartbeat was accepted and recorded by the registry.</summary>
        public bool Acknowledged { get; }

        /// <summary>Authoritative current device status on the server.</summary>
        public DeviceStatus Status { get; }

        /// <summary>Authoritative server timestamp in UTC for clock drift calibration.</summary>
        public DateTimeOffset ServerTimestampUtc { get; }

        /// <summary>Whether the device must initiate a full synchronization with backend services.</summary>
        public bool RequiresSync { get; }

        /// <summary>Whether the device registration has been revoked or suspended and must stop privileged operations.</summary>
        public bool RequiresRevocation => Status == DeviceStatus.Revoked || Status == DeviceStatus.Suspended;

        /// <summary>Optional administrative instruction or message dispatched to the client.</summary>
        public string? Instructions { get; }

        /// <summary>Estimated offline grace period remaining before full re-authentication is mandatory.</summary>
        public TimeSpan? GracePeriodRemaining { get; }

        public DeviceHeartbeatResult(
            bool acknowledged,
            DeviceStatus status,
            DateTimeOffset? serverTimestampUtc = null,
            bool requiresSync = false,
            string? instructions = null,
            TimeSpan? gracePeriodRemaining = null)
        {
            Acknowledged = acknowledged;
            Status = status;
            ServerTimestampUtc = serverTimestampUtc ?? DateTimeOffset.UtcNow;
            RequiresSync = requiresSync;
            Instructions = instructions;
            GracePeriodRemaining = gracePeriodRemaining;
        }

        public static DeviceHeartbeatResult Active(TimeSpan? graceRemaining = null, bool requiresSync = false) =>
            new DeviceHeartbeatResult(true, DeviceStatus.Active, DateTimeOffset.UtcNow, requiresSync, null, graceRemaining);

        public static DeviceHeartbeatResult Revoked(string reason) =>
            new DeviceHeartbeatResult(true, DeviceStatus.Revoked, DateTimeOffset.UtcNow, false, reason);

        public static DeviceHeartbeatResult Suspended(string reason) =>
            new DeviceHeartbeatResult(true, DeviceStatus.Suspended, DateTimeOffset.UtcNow, false, reason);
    }
}
