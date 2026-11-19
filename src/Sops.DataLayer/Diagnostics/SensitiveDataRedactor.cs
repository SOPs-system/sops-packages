using System;
using System.Text.RegularExpressions;

namespace Sops.DataLayer.Diagnostics
{
    /// <summary>
    /// Sanitizes and redacts sensitive data (passwords, tokens, cards, national IDs)
    /// from logs, exceptions, and diagnostics dumps.
    /// </summary>
    public static class SensitiveDataRedactor
    {
        private const string Mask = "***REDACTED***";

        private static readonly Regex TokenRegex = new Regex(@"(?i)(bearer\s+|token[""']?\s*[:=]\s*[""']?)([a-zA-Z0-9_\-\.]{15,})", RegexOptions.Compiled);
        private static readonly Regex PasswordRegex = new Regex(@"(?i)(""?(password|pin|secret|apikey|client_secret)""?\s*[:=]\s*""?)([^"",\r\n}]+)", RegexOptions.Compiled);
        private static readonly Regex CardRegex = new Regex(@"\b(?:\d[ -]*?){13,16}\b", RegexOptions.Compiled);
        private static readonly Regex NationalIdRegex = new Regex(@"\b[23]\d{13}\b", RegexOptions.Compiled); // Egyptian 14-digit national ID

        public static string Redact(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string result = TokenRegex.Replace(input, $"$1{Mask}");
            result = PasswordRegex.Replace(result, $"$1{Mask}");
            result = CardRegex.Replace(result, Mask);
            result = NationalIdRegex.Replace(result, Mask);

            return result;
        }

        public static string MaskHeader(string headerName, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            if (string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerName, "X-Session-Token", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerName, "X-Api-Key", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Length > 10)
                {
                    return value.Substring(0, 6) + "..." + Mask;
                }
                return Mask;
            }

            return value;
        }
    }
}
