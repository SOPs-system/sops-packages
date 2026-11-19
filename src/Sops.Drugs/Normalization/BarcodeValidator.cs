using System;
using System.Text.RegularExpressions;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Validates and normalizes pharmaceutical barcodes (EAN-13, EAN-8, UPC-A, GTIN-14).
    /// </summary>
    public static class BarcodeValidator
    {
        private static readonly Regex DigitsOnlyRegex = new Regex(@"^\d+$", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes a raw barcode string, trimming whitespace and preserving leading zeros.
        /// </summary>
        /// <param name="rawBarcode">The raw barcode text.</param>
        /// <returns>A normalized digit string, or <c>null</c> if invalid.</returns>
        public static string? Normalize(string? rawBarcode)
        {
            if (string.IsNullOrWhiteSpace(rawBarcode))
                return null;

            string cleaned = rawBarcode!.Trim();

            // Strip floating point decimals if dataset serialized integer as "6221000003295.0"
            if (cleaned.EndsWith(".0", StringComparison.Ordinal))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 2);
            }

            if (!DigitsOnlyRegex.IsMatch(cleaned))
                return null;

            // Valid barcode lengths in retail and pharmaceutical distribution: 8, 12, 13, 14
            if (cleaned.Length != 8 && cleaned.Length != 12 && cleaned.Length != 13 && cleaned.Length != 14)
            {
                return null;
            }

            return cleaned;
        }

        /// <summary>
        /// Validates the Modulo 10 check digit for standard EAN/UPC/GTIN barcodes.
        /// </summary>
        /// <param name="barcode">The normalized digit string.</param>
        /// <returns><c>true</c> if check digit is mathematically correct; otherwise, <c>false</c>.</returns>
        public static bool ValidateCheckDigit(string? barcode)
        {
            string? normalized = Normalize(barcode);
            if (normalized == null || normalized.Length < 8)
                return false;

            int len = normalized.Length;
            int checkDigit = normalized[len - 1] - '0';

            int sum = 0;
            // Weigh digits alternating 3 and 1 starting from the digit immediately preceding the check digit, moving right-to-left
            bool multiplyByThree = true;
            for (int i = len - 2; i >= 0; i--)
            {
                int digit = normalized[i] - '0';
                sum += multiplyByThree ? digit * 3 : digit;
                multiplyByThree = !multiplyByThree;
            }

            int computedCheckDigit = (10 - (sum % 10)) % 10;
            return checkDigit == computedCheckDigit;
        }
    }
}
