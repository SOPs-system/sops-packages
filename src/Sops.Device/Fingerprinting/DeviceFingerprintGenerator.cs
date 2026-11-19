using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sops.Device.Models;

namespace Sops.Device.Fingerprinting
{
    /// <summary>
    /// Generates versioned, deterministic device fingerprints from collected signals.
    /// Supports both Version 1 (unkeyed SHA-256 with application salt) for backward compatibility
    /// and Version 2 (keyed HMAC-SHA256 with injected secret key) for protection against offline guessing.
    /// </summary>
    public static class DeviceFingerprintGenerator
    {
        /// <summary>Version 1 algorithm: Unkeyed SHA-256 with application salt.</summary>
        public const int Version1 = 1;

        /// <summary>Version 2 algorithm: Keyed HMAC-SHA256 with injected tenant or application secret key.</summary>
        public const int Version2 = 2;

        /// <summary>Default algorithm version for new implementations.</summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Generates a deterministic <see cref="DeviceFingerprint"/> using Version 1 unkeyed SHA-256 with salt.
        /// Preserved for backward compatibility with existing license bindings.
        /// </summary>
        /// <param name="signals">Evaluated device signals.</param>
        /// <param name="version">Fingerprint algorithm version.</param>
        /// <param name="salt">Optional application salt or tenant domain key.</param>
        /// <returns>Computed device fingerprint.</returns>
        public static DeviceFingerprint Generate(IEnumerable<DeviceSignal> signals, int version = Version1, string? salt = null)
        {
            return Generate(signals, version, secretKey: null, scope: salt, keyScope: KeyScope.Application, algorithm: FingerprintAlgorithm.Sha256Unkeyed);
        }

        /// <summary>
        /// Generates a deterministic <see cref="DeviceFingerprint"/> using Version 2 keyed HMAC-SHA256 with a private secret key.
        /// Protects low-entropy hardware identifiers from offline dictionary attacks.
        /// </summary>
        /// <param name="signals">Evaluated device signals.</param>
        /// <param name="secretKey">Cryptographic secret key for HMAC-SHA256.</param>
        /// <param name="scope">Domain scope (tenant or application identifier).</param>
        /// <param name="keyScope">Key scoping boundary.</param>
        /// <param name="version">Fingerprint algorithm version (defaults to 2).</param>
        /// <returns>Computed keyed device fingerprint.</returns>
        public static DeviceFingerprint GenerateKeyed(
            IEnumerable<DeviceSignal> signals,
            byte[] secretKey,
            string scope = "default",
            KeyScope keyScope = KeyScope.Application,
            int version = Version2)
        {
            if (secretKey == null || secretKey.Length == 0)
                throw new ArgumentException("Secret key cannot be null or empty for keyed fingerprinting.", nameof(secretKey));

            return Generate(signals, version, secretKey, scope, keyScope, FingerprintAlgorithm.HmacSha256Keyed);
        }

        /// <summary>
        /// Unified fingerprint generator supporting both unkeyed and keyed HMAC hashing strategies.
        /// </summary>
        public static DeviceFingerprint Generate(
            IEnumerable<DeviceSignal> signals,
            int version,
            byte[]? secretKey,
            string? scope,
            KeyScope keyScope,
            FingerprintAlgorithm algorithm)
        {
            string effectiveScope = string.IsNullOrEmpty(scope) ? "default" : scope!;
            bool isKeyed = algorithm == FingerprintAlgorithm.HmacSha256Keyed && secretKey != null && secretKey.Length > 0;

            if (signals == null)
            {
                return DeviceFingerprint.Empty(version, effectiveScope, algorithm, keyScope);
            }

            var signalDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var includedSignals = new List<string>();

            // Filter for available, validated signals
            foreach (var signal in signals)
            {
                if (signal.IsAvailable)
                {
                    string signalHash;
                    if (isKeyed && !string.IsNullOrEmpty(signal.NormalizedValue))
                    {
                        signalHash = DeviceSignalNormalizer.ComputeHmacSha256(signal.NormalizedValue!, secretKey!);
                    }
                    else if (!string.IsNullOrEmpty(signal.ValueHash))
                    {
                        signalHash = signal.ValueHash!;
                    }
                    else if (!string.IsNullOrEmpty(signal.NormalizedValue))
                    {
                        signalHash = DeviceSignalNormalizer.ComputeSha256(signal.NormalizedValue!, effectiveScope);
                    }
                    else
                    {
                        continue;
                    }

                    signalDict[signal.Name] = signalHash;
                    includedSignals.Add(signal.Name);
                }
            }

            if (signalDict.Count == 0)
            {
                return DeviceFingerprint.Empty(version, effectiveScope, algorithm, keyScope);
            }

            // Sort canonically by signal name for deterministic ordering
            var sortedEntries = signalDict.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            var canonicalBuilder = new StringBuilder();
            canonicalBuilder.Append("v=").Append(version).Append(';')
                            .Append("s=").Append(effectiveScope).Append(';');

            if (version >= 2 || isKeyed)
            {
                canonicalBuilder.Append("a=").Append(isKeyed ? "hmac-sha256" : "sha256").Append(';');
            }

            for (int i = 0; i < sortedEntries.Count; i++)
            {
                if (i > 0) canonicalBuilder.Append('|');
                canonicalBuilder.Append(sortedEntries[i].Key)
                                .Append(':')
                                .Append(sortedEntries[i].Value);
            }

            string combinedHash;
            if (isKeyed)
            {
                combinedHash = DeviceSignalNormalizer.ComputeHmacSha256(canonicalBuilder.ToString(), secretKey!);
            }
            else
            {
                combinedHash = DeviceSignalNormalizer.ComputeSha256(canonicalBuilder.ToString(), effectiveScope);
            }

            return new DeviceFingerprint(
                combinedHash: combinedHash,
                signalHashes: signalDict,
                includedSignals: includedSignals,
                fingerprintVersion: version,
                scope: effectiveScope,
                generatedAtUtc: DateTimeOffset.UtcNow,
                algorithm: algorithm,
                keyScope: keyScope,
                isKeyed: isKeyed);
        }
    }
}
