using System;
using Sops.Device.Models;

namespace Sops.Device.Management
{
    /// <summary>
    /// Idempotent device registration payload sent by an application installation to register with the SOPs backend.
    /// Includes unique idempotency key for replay-safe retry handling and deduplication.
    /// </summary>
    public sealed class DeviceRegistrationRequest
    {
        /// <summary>
        /// Unique client-generated token ensuring that network retries are processed idempotently.
        /// Derived from the original registration intent, not from transient request attempt counters.
        /// </summary>
        public string IdempotencyToken { get; }

        /// <summary>Crockford Base32 readable device support code.</summary>
        public string DeviceCode { get; }

        /// <summary>Persistent local installation GUID.</summary>
        public string InstallationId { get; }

        /// <summary>Requested user-friendly display name.</summary>
        public string DisplayName { get; }

        /// <summary>Operating system platform.</summary>
        public DevicePlatform Platform { get; }

        /// <summary>Initial pharmacy tenant and organizational ownership boundary.</summary>
        public DeviceOwnership Ownership { get; }

        /// <summary>Reported system and host application metadata.</summary>
        public DeviceMetadata Metadata { get; }

        /// <summary>Optional hardware fingerprint snapshot for hardware drift auditing.</summary>
        public DeviceFingerprint? Fingerprint { get; }

        /// <summary>Client timestamp in UTC when registration was initiated.</summary>
        public DateTimeOffset RequestedAtUtc { get; }

        public DeviceRegistrationRequest(
            string idempotencyToken,
            string deviceCode,
            string installationId,
            string displayName,
            DevicePlatform platform,
            DeviceOwnership ownership,
            DeviceMetadata? metadata = null,
            DeviceFingerprint? fingerprint = null,
            DateTimeOffset? requestedAtUtc = null)
        {
            IdempotencyToken = !string.IsNullOrWhiteSpace(idempotencyToken)
                ? idempotencyToken.Trim()
                : throw new ArgumentException("IdempotencyToken is required for idempotent registration.", nameof(idempotencyToken));
            DeviceCode = !string.IsNullOrWhiteSpace(deviceCode)
                ? deviceCode.Trim()
                : throw new ArgumentException("DeviceCode is required.", nameof(deviceCode));
            InstallationId = !string.IsNullOrWhiteSpace(installationId)
                ? installationId.Trim()
                : throw new ArgumentException("InstallationId is required.", nameof(installationId));
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : deviceCode;
            Platform = platform;
            Ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            Metadata = metadata ?? new DeviceMetadata(osPlatform: platform);
            Fingerprint = fingerprint;
            RequestedAtUtc = requestedAtUtc ?? DateTimeOffset.UtcNow;
        }
    }
}
