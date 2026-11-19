using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sops.WhatsApp.Security
{
    /// <summary>
    /// Security and cryptographic utility methods for WhatsApp Cloud API operations.
    /// </summary>
    public static class WhatsAppSecurityHelper
    {
        private static readonly Regex NonDigitRegex = new Regex(@"[^\d]", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes a recipient phone number for Meta Cloud API by removing leading '+', spaces, dashes, or parentheses.
        /// </summary>
        /// <param name="phoneNumber">The raw phone number string.</param>
        /// <returns>A string containing only numeric digits.</returns>
        public static string NormalizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return string.Empty;
            }

            return NonDigitRegex.Replace(phoneNumber, string.Empty);
        }

        /// <summary>
        /// Masks a phone number for safe diagnostic and log output (e.g. "201*****4567").
        /// </summary>
        /// <param name="phoneNumber">The phone number to mask.</param>
        /// <returns>A masked phone number string.</returns>
        public static string MaskPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return "[EMPTY]";
            }

            string clean = phoneNumber!.Trim();
            if (clean.Length <= 4)
            {
                return new string('*', clean.Length);
            }

            if (clean.Length <= 7)
            {
                return clean.Substring(0, 2) + new string('*', clean.Length - 4) + clean.Substring(clean.Length - 2);
            }

            // For typical phone numbers (e.g. 10-14 digits): show first 3, mask middle, show last 4
            return clean.Substring(0, 3) + "*****" + clean.Substring(clean.Length - 4);
        }

        /// <summary>
        /// Masks a sensitive secret or token for safe log and error output.
        /// </summary>
        /// <param name="token">The secret string to mask.</param>
        /// <returns>A masked string.</returns>
        public static string MaskToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return "[REDACTED]";
            }

            if (token!.Length <= 6)
            {
                return "******";
            }

            return token.Substring(0, 3) + "..." + token.Substring(token.Length - 3);
        }

        /// <summary>
        /// Computes the HMAC-SHA256 signature for a raw payload using the specified secret key.
        /// </summary>
        /// <param name="payload">The raw payload string.</param>
        /// <param name="secret">The shared secret key.</param>
        /// <returns>A lower-case hexadecimal representation of the computed HMAC-SHA256 hash.</returns>
        public static string ComputeHmacSha256(string payload, string secret)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(payloadBytes);
                var sb = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Verifies a Meta webhook signature header (format: "sha256={hash}") against raw payload content using constant-time comparison.
        /// </summary>
        /// <param name="signatureHeader">The value of the X-Hub-Signature-256 HTTP header.</param>
        /// <param name="rawPayload">The raw JSON request body as a string.</param>
        /// <param name="appSecret">The Meta App Secret configured for the webhook.</param>
        /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
        public static bool VerifyWebhookSignature(string? signatureHeader, string? rawPayload, string? appSecret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) ||
                string.IsNullOrWhiteSpace(rawPayload) ||
                string.IsNullOrWhiteSpace(appSecret))
            {
                return false;
            }

            string trimmedHeader = signatureHeader!.Trim();
            const string prefix = "sha256=";
            if (!trimmedHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string expectedHex = trimmedHeader.Substring(prefix.Length).Trim();
            string computedHex = ComputeHmacSha256(rawPayload!, appSecret!);

            return ConstantTimeEquals(computedHex, expectedHex);
        }

        /// <summary>
        /// Compares two strings in constant time to prevent timing analysis attacks.
        /// </summary>
        /// <param name="a">The first string.</param>
        /// <param name="b">The second string.</param>
        /// <returns><c>true</c> if both strings are equal; otherwise, <c>false</c>.</returns>
        public static bool ConstantTimeEquals(string? a, string? b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        /// <summary>
        /// Validates a Meta webhook verification challenge request (HTTP GET) and returns the challenge value if valid.
        /// </summary>
        /// <param name="mode">The hub.mode query parameter.</param>
        /// <param name="token">The hub.verify_token query parameter.</param>
        /// <param name="challenge">The hub.challenge query parameter.</param>
        /// <param name="configuredVerifyToken">The secret verify token configured in application settings.</param>
        /// <param name="challengeResponse">Receives the challenge string to echo back when validation succeeds.</param>
        /// <returns><c>true</c> if the challenge parameters are valid; otherwise, <c>false</c>.</returns>
        public static bool TryVerifyWebhookChallenge(
            string? mode,
            string? token,
            string? challenge,
            string? configuredVerifyToken,
            out string? challengeResponse)
        {
            challengeResponse = null;

            if (string.IsNullOrWhiteSpace(mode) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(challenge) ||
                string.IsNullOrWhiteSpace(configuredVerifyToken))
            {
                return false;
            }

            if (!string.Equals(mode, "subscribe", StringComparison.Ordinal))
            {
                return false;
            }

            if (!ConstantTimeEquals(token!, configuredVerifyToken!))
            {
                return false;
            }

            challengeResponse = challenge;
            return true;
        }
    }
}
