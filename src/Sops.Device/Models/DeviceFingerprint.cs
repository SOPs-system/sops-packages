using System;
using System.Collections.Generic;
using Sops.Device.Fingerprinting;

namespace Sops.Device.Models
{
    /// <summary>
    /// Versioned cryptographic fingerprint calculated from normalized hardware and system signals.
    /// Provides supplementary hardware-change evidence; not an authentication credential.
    /// </summary>
    public sealed class DeviceFingerprint
    {
        /// <summary>Fingerprint algorithm version.</summary>
        public int FingerprintVersion { get; }

        /// <summary>Cryptographic algorithm used to compute signal hashes and combined fingerprint.</summary>
        public FingerprintAlgorithm Algorithm { get; }

        /// <summary>Key scoping boundary (Installation, Application, Tenant, Server).</summary>
        public KeyScope KeyScope { get; }

        /// <summary>Whether this fingerprint was computed using a private secret key (HMAC).</summary>
        public bool IsKeyed { get; }

        /// <summary>Salted application or tenant domain scope for this fingerprint.</summary>
        public string Scope { get; }

        /// <summary>Deterministic combined hash of all canonical hardware signals.</summary>
        public string CombinedHash { get; }

        /// <summary>Individual hashes for each evaluated signal.</summary>
        public IReadOnlyDictionary<string, string> SignalHashes { get; }

        /// <summary>Names of signals successfully included in the combined hash.</summary>
        public IReadOnlyList<string> IncludedSignals { get; }

        /// <summary>Timestamp in UTC when the fingerprint was computed.</summary>
        public DateTimeOffset GeneratedAtUtc { get; }

        public DeviceFingerprint(
            string combinedHash,
            IReadOnlyDictionary<string, string> signalHashes,
            IReadOnlyList<string> includedSignals,
            int fingerprintVersion = 1,
            string scope = "default",
            DateTimeOffset? generatedAtUtc = null,
            FingerprintAlgorithm algorithm = FingerprintAlgorithm.Sha256Unkeyed,
            KeyScope keyScope = KeyScope.Application,
            bool isKeyed = false)
        {
            CombinedHash = combinedHash ?? throw new ArgumentNullException(nameof(combinedHash));
            SignalHashes = signalHashes ?? new Dictionary<string, string>();
            IncludedSignals = includedSignals ?? Array.Empty<string>();
            FingerprintVersion = fingerprintVersion;
            Scope = scope ?? "default";
            GeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.UtcNow;
            Algorithm = algorithm;
            KeyScope = keyScope;
            IsKeyed = isKeyed;
        }

        public static DeviceFingerprint Empty(int version = 1, string scope = "default", FingerprintAlgorithm algorithm = FingerprintAlgorithm.Sha256Unkeyed, KeyScope keyScope = KeyScope.Application) =>
            new DeviceFingerprint(string.Empty, new Dictionary<string, string>(), Array.Empty<string>(), version, scope, null, algorithm, keyScope, false);
    }
}
