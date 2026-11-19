using System;

namespace Sops.Device.Models
{
    /// <summary>
    /// Complete identity definition of a device, comprising persistent installation GUID,
    /// human-readable support code, and optional hardware fingerprint.
    /// </summary>
    public sealed class DeviceIdentity
    {
        /// <summary>Cryptographically random UUID identifying this software installation.</summary>
        public string InstallationId { get; }

        /// <summary>Human-readable unambiguous device code (e.g. SOP-WIN-7K2M-9Q4D).</summary>
        public string DeviceCode { get; }

        /// <summary>Identity evaluation mode used to generate this identity.</summary>
        public DeviceIdentityMode Mode { get; }

        /// <summary>Host operating system platform.</summary>
        public DevicePlatform Platform { get; }

        /// <summary>Optional hardware fingerprint when hardware or hybrid mode is active.</summary>
        public DeviceFingerprint? Fingerprint { get; }

        /// <summary>Timestamp when this identity was first created on the device.</summary>
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>Timestamp of last validation / evaluation.</summary>
        public DateTimeOffset LastValidatedAtUtc { get; }

        public DeviceIdentity(
            string installationId,
            string deviceCode,
            DeviceIdentityMode mode,
            DevicePlatform platform,
            DeviceFingerprint? fingerprint = null,
            DateTimeOffset? createdAtUtc = null,
            DateTimeOffset? lastValidatedAtUtc = null)
        {
            InstallationId = installationId ?? throw new ArgumentNullException(nameof(installationId));
            DeviceCode = deviceCode ?? throw new ArgumentNullException(nameof(deviceCode));
            Mode = mode;
            Platform = platform;
            Fingerprint = fingerprint;
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
            LastValidatedAtUtc = lastValidatedAtUtc ?? DateTimeOffset.UtcNow;
        }
    }
}
