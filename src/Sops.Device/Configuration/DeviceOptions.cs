using System;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Configuration
{
    /// <summary>
    /// Configuration options for the SOPs device identity and hardware signal SDK.
    /// </summary>
    public sealed class DeviceOptions
    {
        /// <summary>
        /// Prefix prepended to human-readable device codes. Defaults to "SOP".
        /// </summary>
        public string CodePrefix { get; set; } = "SOP";

        /// <summary>
        /// Identity mode determining whether identity is based on installation ID, hardware fingerprint, or hybrid.
        /// Defaults to <see cref="DeviceIdentityMode.Hybrid"/>.
        /// </summary>
        public DeviceIdentityMode IdentityMode { get; set; } = DeviceIdentityMode.Hybrid;

        /// <summary>
        /// Whether hardware signal collection and fingerprint computation are enabled. Defaults to <c>true</c>.
        /// </summary>
        public bool EnableHardwareFingerprint { get; set; } = true;

        /// <summary>
        /// Whether physical network interface MAC addresses are included in hardware signals. Defaults to <c>false</c> for privacy.
        /// </summary>
        public bool IncludeMacAddresses { get; set; } = false;

        /// <summary>
        /// Maximum number of physical network adapters to include in hardware signal collection. Defaults to 2.
        /// </summary>
        public int MaxNetworkAdapters { get; set; } = 2;

        /// <summary>
        /// Maximum combined weight contribution of network adapters in hardware drift scoring.
        /// Prevents multi-adapter systems or USB dongles from dominating core motherboard/disk anchors. Defaults to 2.0.
        /// </summary>
        public double MaxNetworkWeightCap { get; set; } = 2.0;

        /// <summary>
        /// Application or tenant domain salt used to scope and salt hardware signal hashes for unkeyed Version 1 fingerprints.
        /// Preserved for backward compatibility with existing license bindings.
        /// Defaults to "sops:device:v1".
        /// </summary>
        public string ApplicationSalt { get; set; } = "sops:device:v1";

        /// <summary>
        /// Optional cryptographic secret key used for keyed HMAC-SHA256 fingerprinting (Version 2).
        /// When provided, fingerprint computation utilizes keyed HMAC, protecting low-entropy hardware
        /// identifiers (e.g. MAC addresses, BIOS dates) against offline dictionary and rainbow table attacks.
        /// </summary>
        public byte[]? SecretKey { get; set; }

        /// <summary>
        /// Convenience property to get or set <see cref="SecretKey"/> as a hexadecimal string.
        /// </summary>
        public string? SecretKeyHex
        {
            get => SecretKey != null && SecretKey.Length > 0 ? ToHexString(SecretKey) : null;
            set => SecretKey = !string.IsNullOrWhiteSpace(value) ? FromHexString(value!) : null;
        }

        /// <summary>
        /// Key scoping boundary defining whether the secret key is scoped per installation, application, or tenant.
        /// Defaults to <see cref="KeyScope.Application"/>.
        /// </summary>
        public KeyScope KeyScope { get; set; } = KeyScope.Application;

        /// <summary>
        /// Cryptographic algorithm used for hardware fingerprint generation.
        /// Defaults to <see cref="FingerprintAlgorithm.HmacSha256Keyed"/> when <see cref="SecretKey"/> is set,
        /// otherwise <see cref="FingerprintAlgorithm.Sha256Unkeyed"/> for legacy compatibility.
        /// </summary>
        public FingerprintAlgorithm FingerprintAlgorithm { get; set; } = FingerprintAlgorithm.Sha256Unkeyed;

        /// <summary>
        /// Fingerprint algorithm version. Defaults to 1 for unkeyed compatibility, or 2 for keyed HMAC.
        /// </summary>
        public int FingerprintVersion { get; set; } = 1;

        private static string ToHexString(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private static byte[]? FromHexString(string hex)
        {
            string clean = hex.Trim();
            if (clean.Length % 2 != 0) return null;
            byte[] bytes = new byte[clean.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(clean.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                {
                    return null;
                }
            }
            return bytes;
        }

        /// <summary>
        /// Whether hardware drift and component change detection are evaluated. Defaults to <c>true</c>.
        /// </summary>
        public bool EnableChangeDetection { get; set; } = true;

        /// <summary>
        /// Optional custom file path or directory for identity persistence.
        /// </summary>
        public string? StoragePath { get; set; }

        /// <summary>
        /// Optional simulated or override platform for testing and alternative hosting environments.
        /// </summary>
        public DevicePlatform? CustomPlatform { get; set; }

        /// <summary>
        /// Optional host application version string to include in device metadata.
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        /// Matching tolerance (0.0 to 1.0) for fuzzy hardware comparison. Defaults to 0.7 (70%).
        /// </summary>
        public double Tolerance { get; set; } = 0.7;

        /// <summary>
        /// Extensibility hook allowing host applications (e.g. .NET MAUI or Xamarin on Android/iOS)
        /// to supply platform-native signals dynamically without requiring mobile build workloads on desktop consumers.
        /// </summary>
        public Func<string, string?>? NativeSignalResolver { get; set; }

        /// <summary>
        /// Number of retry attempts when encountering transient file sharing / storage lock contention. Defaults to 3.
        /// </summary>
        public int StorageRetryCount { get; set; } = 3;

        /// <summary>
        /// Delay in milliseconds between storage retry attempts. Defaults to 100ms.
        /// </summary>
        public int StorageRetryDelayMs { get; set; } = 100;
    }
}
