using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Sops.Device.Models;

namespace Sops.Device.Fingerprinting
{
    /// <summary>
    /// Normalizes, validates, sanitizes, and hashes hardware and system signals.
    /// Rejects known OEM placeholders, invalid serial patterns, all-zero values, and virtual adapter artifacts.
    /// </summary>
    public static class DeviceSignalNormalizer
    {
        private static readonly HashSet<string> KnownPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "to be filled by o.e.m.",
            "to be filled by oem",
            "to be filled by o.e.m",
            "default string",
            "none",
            "n/a",
            "na",
            "null",
            "system serial number",
            "system manufacturer",
            "system product name",
            "chassis serial number",
            "base board serial number",
            "motherboard serial number",
            "standard pc",
            "all series",
            "123456789",
            "1234567890",
            "0123456789",
            "not specified",
            "unknown",
            "empty",
            "invalid",
            "paravirtual",
            "qemu",
            "bochs",
            "hyper-v",
            "vmware-",
            "virtualbox"
        };

        private static readonly Regex MacCleaningRegex = new Regex(@"[^0-9A-Fa-f]", RegexOptions.Compiled);

        /// <summary>
        /// Validates a raw hardware or system signal string.
        /// </summary>
        /// <param name="name">Signal name.</param>
        /// <param name="rawValue">Raw value collected from the OS.</param>
        /// <param name="rejectionReason">Output explanation if rejected.</param>
        /// <returns><c>true</c> if valid; <c>false</c> if blank, placeholder, or invalid.</returns>
        public static bool IsValidSignal(string name, string? rawValue, out string? rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                rejectionReason = "Signal value is null or empty.";
                return false;
            }

            string trimmed = rawValue!.Trim();

            // Check minimum length
            if (trimmed.Length < 2)
            {
                rejectionReason = "Signal value is too short.";
                return false;
            }

            // Check known placeholders and virtual machine prefixes
            foreach (var placeholder in KnownPlaceholders)
            {
                if (trimmed.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                    (placeholder.EndsWith("-") && trimmed.StartsWith(placeholder, StringComparison.OrdinalIgnoreCase)) ||
                    trimmed.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rejectionReason = $"Matched known OEM placeholder or virtualization artifact: '{trimmed}'.";
                    return false;
                }
            }

            // Check all-zeros / all-ones / repeated single character
            if (IsAllZerosOrRepeatedChar(trimmed))
            {
                rejectionReason = "Signal value consists exclusively of zeros, dashes, or repetitive characters.";
                return false;
            }

            // Specific validation for UUIDs
            if (name.IndexOf("uuid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("guid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Guid.TryParse(trimmed, out var guid))
                {
                    if (guid == Guid.Empty)
                    {
                        rejectionReason = "GUID signal is all-zeros (Guid.Empty).";
                        return false;
                    }
                }
            }

            // Specific validation for MAC addresses
            if (name.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string cleanMac = MacCleaningRegex.Replace(trimmed, string.Empty);
                if (cleanMac.Length != 12)
                {
                    rejectionReason = $"MAC address '{trimmed}' has invalid length ({cleanMac.Length} hex characters).";
                    return false;
                }

                if (IsAllZerosOrRepeatedChar(cleanMac))
                {
                    rejectionReason = "MAC address is all zeros or all Fs.";
                    return false;
                }

                // Check IEEE 802 multicast bit (least significant bit of first octet is 1, e.g. 01:00:5E:... or 33:33:...)
                if (byte.TryParse(cleanMac.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte firstByte))
                {
                    if ((firstByte & 0x01) == 0x01)
                    {
                        rejectionReason = "MAC address is an IEEE 802 multicast address (Group/Multicast bit is set).";
                        return false;
                    }
                }
            }

            rejectionReason = null;
            return true;
        }

        /// <summary>
        /// Normalizes a signal value for deterministic comparison across platforms and drivers.
        /// </summary>
        public static string Normalize(string name, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return string.Empty;

            string trimmed = rawValue.Trim();

            // If MAC address, strip separators and format as uppercase 12-char hex
            if (name.IndexOf("mac", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string cleanMac = MacCleaningRegex.Replace(trimmed, string.Empty).ToUpperInvariant();
                if (cleanMac.Length == 12)
                {
                    return $"{cleanMac.Substring(0, 2)}:{cleanMac.Substring(2, 2)}:{cleanMac.Substring(4, 2)}:{cleanMac.Substring(6, 2)}:{cleanMac.Substring(8, 2)}:{cleanMac.Substring(10, 2)}";
                }
                return cleanMac;
            }

            // If GUID/UUID, format as lowercase D (hyphenated)
            if ((name.IndexOf("uuid", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("guid", StringComparison.OrdinalIgnoreCase) >= 0) &&
                Guid.TryParse(trimmed, out var guid))
            {
                return guid.ToString("D").ToLowerInvariant();
            }

            // Standard normalization: trim and normalize internal consecutive whitespace
            var sb = new StringBuilder(trimmed.Length);
            bool lastWasSpace = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Computes the SHA-256 hexadecimal hash of the normalized signal value with optional application/tenant domain salt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Security Notice on Low-Entropy Hardware Identifiers:</b><br/>
        /// Hardware values such as MAC addresses (48 bits, of which 24 bits are publicly known IEEE OUIs and 24 bits
        /// represent device serials) possess approximately 24 bits of variable entropy (~16.7 million combinations).
        /// Other components like BIOS release dates or CPU model strings have even lower entropy (&lt;16 bits).
        /// </para>
        /// <para>
        /// An unkeyed hash function (even when combined with a fixed, public salt like <c>"sops:device:v1"</c>)
        /// allows an offline attacker with a leaked hash database to exhaust all 16.7 million MAC combinations
        /// in seconds using consumer GPU/CPU hardware.
        /// </para>
        /// <para>
        /// For genuine offline guessing protection, applications must use <see cref="ComputeHmacSha256(string, byte[])"/>
        /// with a securely provisioned application or tenant secret key.
        /// </para>
        /// </remarks>
        /// <param name="normalizedValue">Normalized hardware signal value.</param>
        /// <param name="salt">Optional application salt or tenant domain key.</param>
        /// <returns>Hex-encoded SHA-256 hash string.</returns>
        public static string ComputeSha256(string normalizedValue, string? salt = null)
        {
            if (string.IsNullOrEmpty(normalizedValue)) return string.Empty;

            string payload = string.IsNullOrEmpty(salt) ? normalizedValue : $"{salt}:{normalizedValue}";
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Computes a keyed HMAC-SHA256 hexadecimal hash of the normalized signal value.
        /// Keyed HMAC cryptographically binds the hardware value to a private tenant or application secret,
        /// rendering offline rainbow table and dictionary attacks mathematically infeasible even for low-entropy
        /// hardware values (such as network MAC addresses).
        /// </summary>
        /// <param name="normalizedValue">Normalized hardware signal value.</param>
        /// <param name="key">Cryptographic secret key (minimum 16 bytes recommended, 32 bytes optimal).</param>
        /// <returns>Hex-encoded HMAC-SHA256 hash string.</returns>
        public static string ComputeHmacSha256(string normalizedValue, byte[] key)
        {
            if (string.IsNullOrEmpty(normalizedValue)) return string.Empty;
            if (key == null || key.Length == 0) throw new ArgumentException("HMAC key cannot be null or empty.", nameof(key));

            byte[] bytes = Encoding.UTF8.GetBytes(normalizedValue);
            using (var hmac = new HMACSHA256(key))
            {
                byte[] hash = hmac.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Computes signal hash according to the selected algorithm and available secret key or salt.
        /// </summary>
        /// <param name="normalizedValue">Normalized hardware signal value.</param>
        /// <param name="algorithm">Hashing algorithm to apply.</param>
        /// <param name="secretKey">Optional secret key for HMAC-SHA256.</param>
        /// <param name="salt">Optional domain salt for unkeyed SHA-256.</param>
        /// <returns>Hex-encoded hash string.</returns>
        public static string ComputeSignalHash(string normalizedValue, FingerprintAlgorithm algorithm, byte[]? secretKey, string? salt)
        {
            if (string.IsNullOrEmpty(normalizedValue)) return string.Empty;

            if (algorithm == FingerprintAlgorithm.HmacSha256Keyed && secretKey != null && secretKey.Length > 0)
            {
                return ComputeHmacSha256(normalizedValue, secretKey);
            }

            return ComputeSha256(normalizedValue, salt);
        }

        private static bool IsAllZerosOrRepeatedChar(string value)
        {
            if (string.IsNullOrEmpty(value)) return true;

            int meaningfulCount = 0;
            char firstMeaningful = '\0';
            bool allSame = true;
            bool allZerosOrDashes = true;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '-' || c == ':' || c == '.' || c == ' ') continue;

                if (c != '0')
                {
                    allZerosOrDashes = false;
                }

                if (meaningfulCount == 0)
                {
                    firstMeaningful = char.ToUpperInvariant(c);
                }
                else if (char.ToUpperInvariant(c) != firstMeaningful)
                {
                    allSame = false;
                }

                meaningfulCount++;
            }

            return allZerosOrDashes || (meaningfulCount > 0 && allSame);
        }
    }
}
