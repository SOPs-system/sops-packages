using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sops.Payments.Providers.Paymob
{
    /// <summary>
    /// Implements Paymob's official HMAC-SHA512 webhook signature verification.
    /// Concatenates the 20 documented transaction fields in strict lexicographical order and executes constant-time verification.
    /// </summary>
    public static class PaymobHmacValidator
    {
        /// <summary>
        /// The 20 fields required by Paymob in strict lexicographical order.
        /// </summary>
        public static readonly string[] RequiredFieldsInOrder = new[]
        {
            "amount_cents",
            "created_at",
            "currency",
            "error_occured",
            "has_parent_transaction",
            "id",
            "integration_id",
            "is_3d_secure",
            "is_auth",
            "is_capture",
            "is_refunded",
            "is_standalone_payment",
            "is_voided",
            "order.id",
            "owner",
            "pending",
            "source_data.pan",
            "source_data.sub_type",
            "source_data.type",
            "success"
        };

        /// <summary>
        /// Validates an incoming Paymob webhook payload against the expected HMAC signature using the merchant's HMAC secret.
        /// </summary>
        /// <param name="rawJson">The raw JSON webhook payload.</param>
        /// <param name="signature">The expected HMAC hex string received from Paymob (e.g. from the hmac query parameter).</param>
        /// <param name="hmacSecret">The merchant's HMAC secret configured in the Paymob dashboard.</param>
        /// <returns>True if the calculated HMAC matches the provided signature; otherwise, false.</returns>
        public static bool Validate(string rawJson, string? signature, string? hmacSecret)
        {
            if (string.IsNullOrWhiteSpace(rawJson) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(hmacSecret))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;

                // Paymob wraps the transaction in an "obj" container for TRANSACTION events
                JsonElement targetElement = root.TryGetProperty("obj", out var objElement) ? objElement : root;

                string concatenated = BuildConcatenatedString(targetElement);
                string calculatedHmac = ComputeHmacSha512(concatenated, hmacSecret!);

                return ConstantTimeEquals(calculatedHmac, signature!.Trim());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Builds the concatenated string of the 20 Paymob fields from a JSON transaction element.
        /// </summary>
        /// <param name="element">The JSON element representing the transaction object.</param>
        /// <returns>The unseparated concatenated string for hashing.</returns>
        public static string BuildConcatenatedString(JsonElement element)
        {
            var sb = new StringBuilder();

            // 1. amount_cents
            sb.Append(GetStringValue(element, "amount_cents"));

            // 2. created_at
            sb.Append(GetStringValue(element, "created_at"));

            // 3. currency
            sb.Append(GetStringValue(element, "currency"));

            // 4. error_occured
            sb.Append(GetBoolValue(element, "error_occured"));

            // 5. has_parent_transaction
            sb.Append(GetBoolValue(element, "has_parent_transaction"));

            // 6. id
            sb.Append(GetStringValue(element, "id"));

            // 7. integration_id
            sb.Append(GetStringValue(element, "integration_id"));

            // 8. is_3d_secure
            sb.Append(GetBoolValue(element, "is_3d_secure"));

            // 9. is_auth
            sb.Append(GetBoolValue(element, "is_auth"));

            // 10. is_capture
            sb.Append(GetBoolValue(element, "is_capture"));

            // 11. is_refunded
            sb.Append(GetBoolValue(element, "is_refunded"));

            // 12. is_standalone_payment
            sb.Append(GetBoolValue(element, "is_standalone_payment"));

            // 13. is_voided
            sb.Append(GetBoolValue(element, "is_voided"));

            // 14. order.id
            sb.Append(GetNestedStringValue(element, "order", "id"));

            // 15. owner
            sb.Append(GetStringValue(element, "owner"));

            // 16. pending
            sb.Append(GetBoolValue(element, "pending"));

            // 17. source_data.pan
            sb.Append(GetNestedStringValue(element, "source_data", "pan"));

            // 18. source_data.sub_type
            sb.Append(GetNestedStringValue(element, "source_data", "sub_type"));

            // 19. source_data.type
            sb.Append(GetNestedStringValue(element, "source_data", "type"));

            // 20. success
            sb.Append(GetBoolValue(element, "success"));

            return sb.ToString();
        }

        /// <summary>
        /// Computes the HMAC-SHA512 of a string payload using the specified secret key.
        /// </summary>
        /// <param name="data">The concatenated data string.</param>
        /// <param name="key">The HMAC secret.</param>
        /// <returns>The lowercase hex string representation of the computed hash.</returns>
        public static string ComputeHmacSha512(string data, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using var hmac = new HMACSHA512(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(dataBytes);

            var sb = new StringBuilder(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Performs constant-time comparison of two strings to prevent timing attacks.
        /// </summary>
        /// <param name="a">First string.</param>
        /// <param name="b">Second string.</param>
        /// <returns>True if strings are equal (case-insensitive for hex); otherwise false.</returns>
        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                char ca = char.ToLowerInvariant(a[i]);
                char cb = char.ToLowerInvariant(b[i]);
                result |= ca ^ cb;
            }

            return result == 0;
        }

        private static string GetStringValue(JsonElement parent, string propertyName)
        {
            if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString() ?? string.Empty;
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetRawText();
                if (prop.ValueKind == JsonValueKind.True) return "true";
                if (prop.ValueKind == JsonValueKind.False) return "false";
                if (prop.ValueKind == JsonValueKind.Null) return string.Empty;
                return prop.GetRawText();
            }

            return string.Empty;
        }

        private static string GetBoolValue(JsonElement parent, string propertyName)
        {
            if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return "true";
                if (prop.ValueKind == JsonValueKind.False) return "false";
                if (prop.ValueKind == JsonValueKind.String)
                {
                    string str = prop.GetString() ?? string.Empty;
                    if (bool.TryParse(str, out bool parsed))
                    {
                        return parsed ? "true" : "false";
                    }
                    return str.ToLowerInvariant();
                }
            }

            return "false";
        }

        private static string GetNestedStringValue(JsonElement parent, string parentProp, string childProp)
        {
            if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(parentProp, out var nestedObj))
            {
                return GetStringValue(nestedObj, childProp);
            }

            return string.Empty;
        }
    }
}
