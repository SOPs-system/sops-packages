using System;
using System.Collections.Generic;

namespace Sops.Device.Models
{
    /// <summary>
    /// Safe public device metadata suitable for registration, support, diagnostics, and telemetry.
    /// Excludes sensitive raw hardware serial numbers and MAC addresses.
    /// </summary>
    public sealed class DeviceInfo
    {
        /// <summary>Human-readable device registration and support code (e.g. SOP-WIN-7K2M-9Q4D).</summary>
        public string DeviceCode { get; }

        /// <summary>Persistent software installation UUID.</summary>
        public string InstallationId { get; }

        /// <summary>Detected operating system platform.</summary>
        public DevicePlatform Platform { get; }

        /// <summary>Operating system version and description.</summary>
        public string PlatformVersion { get; }

        /// <summary>Processor architecture (e.g. x64, Arm64, x86).</summary>
        public string Architecture { get; }

        /// <summary>Device form-factor classification (e.g. Desktop, Server, Mobile, VirtualMachine).</summary>
        public string DeviceType { get; }

        /// <summary>Optional host application version.</summary>
        public string? AppVersion { get; }

        /// <summary>Fingerprint algorithm version.</summary>
        public int FingerprintVersion { get; }

        /// <summary>Signal collection status overview indicating available, denied, and rejected signals.</summary>
        public IReadOnlyDictionary<string, SignalStatus> SignalAvailability { get; }

        /// <summary>Timestamp in UTC when metadata was captured.</summary>
        public DateTimeOffset CollectedAtUtc { get; }

        public DeviceInfo(
            string deviceCode,
            string installationId,
            DevicePlatform platform,
            string platformVersion,
            string architecture,
            string deviceType,
            int fingerprintVersion,
            IReadOnlyDictionary<string, SignalStatus> signalAvailability,
            string? appVersion = null,
            DateTimeOffset? collectedAtUtc = null)
        {
            DeviceCode = deviceCode ?? throw new ArgumentNullException(nameof(deviceCode));
            InstallationId = installationId ?? throw new ArgumentNullException(nameof(installationId));
            Platform = platform;
            PlatformVersion = platformVersion ?? string.Empty;
            Architecture = architecture ?? string.Empty;
            DeviceType = deviceType ?? "Unknown";
            FingerprintVersion = fingerprintVersion;
            SignalAvailability = signalAvailability ?? new Dictionary<string, SignalStatus>();
            AppVersion = appVersion;
            CollectedAtUtc = collectedAtUtc ?? DateTimeOffset.UtcNow;
        }
    }
}
