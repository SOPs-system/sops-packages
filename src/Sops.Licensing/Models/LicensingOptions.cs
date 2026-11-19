using System;
using System.Collections.Generic;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Preferred secure storage mechanism for the current operating environment.
    /// </summary>
    public enum StorageProviderType
    {
        /// <summary>Automatically detect the best operating-system-backed secure storage provider.</summary>
        Auto,

        /// <summary>Windows Credential Manager (native Advapi32 APIs, best for small credentials).</summary>
        WindowsCredentialManager,

        /// <summary>Windows Data Protection API (DPAPI) file storage (best for application files and larger licenses).</summary>
        WindowsDpapi,

        /// <summary>macOS Keychain Services (native Security.framework APIs).</summary>
        MacKeychain,

        /// <summary>Linux Secret Service (freedesktop D-Bus Secret Service protocol).</summary>
        LinuxSecretService,

        /// <summary>Linux Encrypted Vault (AES-256 with PBKDF2 key derivation for headless environments).</summary>
        LinuxEncryptedVault
    }

    /// <summary>
    /// Configuration options for the SOPs licensing and secure storage engine.
    /// </summary>
    public class LicensingOptions
    {
        /// <summary>Configuration section name for binding (e.g. "Licensing").</summary>
        public const string SectionName = "Licensing";

        /// <summary>Product or application identity to validate licenses against (e.g. "SOPs.Pharmacy").</summary>
        public string ProductId { get; set; } = "SOPs.App";

        /// <summary>Application-scoped namespace partition for secure storage (default: "Sops.Licensing").</summary>
        public string StorageNamespace { get; set; } = "Sops.Licensing";

        /// <summary>Base URL of the online licensing server (e.g. "https://license.sops.local").</summary>
        public string? ServerUrl { get; set; }

        /// <summary>
        /// Dictionary of trusted public keys (Key ID -> PEM or Base64 public key) used to verify certificate digital signatures.
        /// Client applications only ever store public verification keys, never signing private keys.
        /// </summary>
        public Dictionary<string, string> TrustedPublicKeys { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Tolerance ratio for legitimate hardware component changes during device verification (0.0 to 1.0, default 0.7 = 70% match).</summary>
        public double HardwareMatchTolerance { get; set; } = 0.7;

        /// <summary>Maximum allowed clock rollback tolerance in seconds before triggering tampering alert (default: 300 seconds / 5 minutes).</summary>
        public int ClockRollbackToleranceSeconds { get; set; } = 300;

        /// <summary>Preferred storage provider type, or Auto to select according to OS detection.</summary>
        public StorageProviderType StorageProvider { get; set; } = StorageProviderType.Auto;

        /// <summary>Optional directory path for Linux encrypted vault or DPAPI file storage. If omitted, uses local app data folder.</summary>
        public string? StorageDirectory { get; set; }

        /// <summary>Optional passphrase used for Linux headless encrypted vault key derivation.</summary>
        public string? LinuxVaultPassphrase { get; set; }

        /// <summary>
        /// Optional delegate providing current device registration status from Sops.Device.Management.
        /// If provided and the status is Revoked or Suspended, license validation fails even if local hardware signals match.
        /// </summary>
        public Func<Sops.Device.Management.DeviceStatus>? DeviceStatusProvider { get; set; }
    }
}
