using System;
using System.Security.Cryptography;
using System.Text;
using Sops.Device.Abstractions;
using Sops.Device.Models;

namespace Sops.Device.Identity
{
    /// <summary>
    /// Generates and validates versioned, human-readable device codes using Crockford's unambiguous Base32 alphabet
    /// with an integrated mod-32 checksum digit.
    /// </summary>
    public sealed class DeviceCodeGenerator : IDeviceCodeGenerator
    {
        // Crockford's Base32 alphabet: excludes I, L, O, U
        private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string DefaultPrefix = "SOP";

        private readonly string _configuredPrefix;

        public DeviceCodeGenerator(string prefix = DefaultPrefix)
        {
            _configuredPrefix = string.IsNullOrWhiteSpace(prefix) ? DefaultPrefix : prefix.Trim().ToUpperInvariant();
        }

        /// <inheritdoc />
        public string GenerateCode(string installationId, DevicePlatform platform, string? prefix = null)
        {
            if (string.IsNullOrWhiteSpace(installationId))
            {
                throw new ArgumentException("InstallationId cannot be null or empty.", nameof(installationId));
            }

            string actualPrefix = !string.IsNullOrWhiteSpace(prefix)
                ? prefix!.Trim().ToUpperInvariant()
                : _configuredPrefix;

            string platformTag = GetPlatformTag(platform);

            // Deterministic keyed derivation from installation ID using SHA-256
            byte[] inputBytes = Encoding.UTF8.GetBytes($"SOP-DEVICE-v1:{installationId.Trim().ToLowerInvariant()}");
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(inputBytes);
            }

            // Extract 35 bits from the hash to form 7 Crockford Base32 characters (5 bits each)
            ulong bits = ((ulong)hash[0] << 32) |
                         ((ulong)hash[1] << 24) |
                         ((ulong)hash[2] << 16) |
                         ((ulong)hash[3] << 8) |
                         (ulong)hash[4];

            var payloadChars = new char[7];
            for (int i = 6; i >= 0; i--)
            {
                int index = (int)(bits & 0x1F); // lowest 5 bits
                payloadChars[i] = CrockfordAlphabet[index];
                bits >>= 5;
            }

            // Compute mod-32 check character across the 7 payload characters
            char checkChar = ComputeCheckCharacter(payloadChars);

            // Group as 4 - 4 (Part 1: 4 chars, Part 2: 3 chars + checksum)
            string part1 = new string(payloadChars, 0, 4);
            string part2 = $"{payloadChars[4]}{payloadChars[5]}{payloadChars[6]}{checkChar}";

            return $"{actualPrefix}-{platformTag}-{part1}-{part2}";
        }

        /// <inheritdoc />
        public bool ValidateCode(string code, out string? error) => ValidateCode(code, out _, out error);

        /// <inheritdoc />
        public bool ValidateCode(string code, out int version, out string? error)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(code))
            {
                error = "Device code is null or empty.";
                return false;
            }

            // Normalize: replace common confusing characters (O->0, I/L->1), trim, uppercase
            string normalized = NormalizeCode(code);
            string[] parts = normalized.Split('-');

            if (parts.Length != 4)
            {
                error = $"Invalid device code format. Expected 4 hyphen-delimited segments, got {parts.Length}.";
                return false;
            }

            string prefix = parts[0];
            string platformTag = parts[1];
            string part1 = parts[2];
            string part2 = parts[3];

            if (prefix.Length < 2)
            {
                error = $"Prefix '{prefix}' is too short.";
                return false;
            }

            if (part1.Length != 4 || part2.Length != 4)
            {
                error = $"Invalid payload segment lengths. Expected 4 and 4, got {part1.Length} and {part2.Length}.";
                return false;
            }

            string combinedPayload = part1 + part2;
            for (int i = 0; i < combinedPayload.Length; i++)
            {
                char c = combinedPayload[i];
                if (CrockfordAlphabet.IndexOf(c) < 0)
                {
                    error = $"Invalid character '{c}' at position {i + 1} in payload. Only Crockford Base32 characters are allowed.";
                    return false;
                }
            }

            // Verify checksum digit (last character of part 2)
            char[] payload7 = new char[7];
            for (int i = 0; i < 4; i++) payload7[i] = part1[i];
            payload7[4] = part2[0];
            payload7[5] = part2[1];
            payload7[6] = part2[2];

            char expectedCheck = ComputeCheckCharacter(payload7);
            char actualCheck = part2[3];

            if (expectedCheck != actualCheck)
            {
                error = $"Device code checksum mismatch. Expected '{expectedCheck}', got '{actualCheck}'.";
                return false;
            }

            // Detected and verified V1 format
            version = 1;
            error = null;
            return true;
        }

        public static string NormalizeCode(string rawCode)
        {
            if (string.IsNullOrWhiteSpace(rawCode)) return string.Empty;

            string trimmed = rawCode.Trim();
            string[] parts = trimmed.Split('-');

            if (parts.Length >= 3)
            {
                // Format: PREFIX-PLATFORM-PART1-PART2...
                // Preserve PREFIX and PLATFORM without converting semantic letters to digits
                parts[0] = parts[0].ToUpperInvariant();
                parts[1] = parts[1].ToUpperInvariant();

                for (int p = 2; p < parts.Length; p++)
                {
                    parts[p] = NormalizeCrockfordSegment(parts[p]);
                }

                return string.Join("-", parts);
            }

            return NormalizeCrockfordSegment(trimmed);
        }

        private static string NormalizeCrockfordSegment(string segment)
        {
            var sb = new StringBuilder(segment.Length);
            for (int i = 0; i < segment.Length; i++)
            {
                char c = char.ToUpperInvariant(segment[i]);
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static char ComputeCheckCharacter(char[] chars7)
        {
            int sum = 0;
            for (int i = 0; i < chars7.Length; i++)
            {
                int val = CrockfordAlphabet.IndexOf(chars7[i]);
                sum += val * (i + 1);
            }
            int remainder = sum % 32;
            return CrockfordAlphabet[remainder];
        }

        private static string GetPlatformTag(DevicePlatform platform)
        {
            switch (platform)
            {
                case DevicePlatform.Windows: return "WIN";
                case DevicePlatform.Linux: return "LNX";
                case DevicePlatform.MacOS: return "MAC";
                case DevicePlatform.Android: return "AND";
                case DevicePlatform.IOS: return "IOS";
                case DevicePlatform.MacCatalyst: return "CAT";
                default: return "DEV";
            }
        }
    }
}
