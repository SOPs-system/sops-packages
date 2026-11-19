using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Periodic heartbeat ping sent by a registered device to report liveness, status, and application version.
    /// </summary>
    public sealed class DeviceHeartbeat
    {
        /// <summary>Authoritative server registration identifier.</summary>
        public string RegistrationId { get; }

        /// <summary>Crockford Base32 device code.</summary>
        public string DeviceCode { get; }

        /// <summary>Installation GUID.</summary>
        public string InstallationId { get; }

        /// <summary>Monotonically increasing sequence number from the device session.</summary>
        public long SequenceNumber { get; }

        /// <summary>Current running version of the host application.</summary>
        public string? AppVersion { get; }

        /// <summary>Optional short status summary (e.g. "Online", "ProcessingOrders").</summary>
        public string? StatusSummary { get; }

        /// <summary>Client timestamp in UTC when heartbeat was dispatched.</summary>
        public DateTimeOffset TimestampUtc { get; }

        /// <summary>Optional idempotency key for deduplicating retried heartbeat frames.</summary>
        public string? IdempotencyToken { get; }

        public DeviceHeartbeat(
            string registrationId,
            string deviceCode,
            string installationId,
            long sequenceNumber = 1,
            string? appVersion = null,
            string? statusSummary = "Online",
            DateTimeOffset? timestampUtc = null,
            string? idempotencyToken = null)
        {
            RegistrationId = !string.IsNullOrWhiteSpace(registrationId)
                ? registrationId.Trim()
                : throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            DeviceCode = !string.IsNullOrWhiteSpace(deviceCode)
                ? deviceCode.Trim()
                : throw new ArgumentException("DeviceCode is required.", nameof(deviceCode));
            InstallationId = !string.IsNullOrWhiteSpace(installationId)
                ? installationId.Trim()
                : throw new ArgumentException("InstallationId is required.", nameof(installationId));
            SequenceNumber = sequenceNumber;
            AppVersion = appVersion?.Trim();
            StatusSummary = statusSummary?.Trim();
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
            IdempotencyToken = idempotencyToken?.Trim();
        }
    }
}
