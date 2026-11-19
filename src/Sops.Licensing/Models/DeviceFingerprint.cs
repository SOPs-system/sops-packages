using System;
using System.Collections.Generic;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Cryptographic, privacy-preserving device and installation fingerprint used for node-locked license binding.
    /// Combines persistent installation state with hardware signals, hashing individual attributes to protect user privacy.
    /// </summary>
    public class DeviceFingerprint
    {
        /// <summary>Persistent local installation GUID generated on first run.</summary>
        public string InstallationId { get; set; } = string.Empty;

        /// <summary>SHA-256 canonical hash representing the unified device identity.</summary>
        public string CombinedHash { get; set; } = string.Empty;

        /// <summary>Hashed hardware and platform signals (OS identifier, machine GUID, CPU architecture, motherboard hash, hostname hash).</summary>
        public Dictionary<string, string> Signals { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>UTC timestamp when the fingerprint was computed.</summary>
        public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
