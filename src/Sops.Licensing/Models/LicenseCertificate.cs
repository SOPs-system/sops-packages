using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Digitally signed, tamper-evident cryptographic license certificate authorizing application features,
    /// modules, operations, and device bindings.
    /// </summary>
    public class LicenseCertificate
    {
        /// <summary>Format version of the license certificate structure.</summary>
        [JsonPropertyName("version")]
        public int CertificateVersion { get; set; } = 1;

        /// <summary>Unique license identifier (e.g. UUID or GUID).</summary>
        [JsonPropertyName("license_id")]
        public string LicenseId { get; set; } = string.Empty;

        /// <summary>Product or system identifier (e.g. "SOPs.Pharmacy", "SOPs.Core").</summary>
        [JsonPropertyName("product_id")]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Customer, tenant, or licensee identifier.</summary>
        [JsonPropertyName("subject_id")]
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Commercial subscription tier or plan code (e.g. "ENTERPRISE", "PRO").</summary>
        [JsonPropertyName("plan_id")]
        public string PlanId { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the certificate was generated and signed.</summary>
        [JsonPropertyName("issued_at_utc")]
        public DateTimeOffset IssuedAtUtc { get; set; }

        /// <summary>UTC timestamp when the certificate becomes effective.</summary>
        [JsonPropertyName("valid_from_utc")]
        public DateTimeOffset ValidFromUtc { get; set; }

        /// <summary>UTC timestamp when the license expires.</summary>
        [JsonPropertyName("expires_at_utc")]
        public DateTimeOffset ExpiresAtUtc { get; set; }

        /// <summary>UTC timestamp up to which offline operation is authorized without requiring re-activation.</summary>
        [JsonPropertyName("offline_valid_until_utc")]
        public DateTimeOffset OfflineValidUntilUtc { get; set; }

        /// <summary>Optional privacy-preserving SHA-256 device binding hash, or null for floating/unbound licenses.</summary>
        [JsonPropertyName("device_binding")]
        public string? DeviceBinding { get; set; }

        /// <summary>List of granted or explicitly denied entitlement strings (e.g. "database.raw.read", "pos.*", "!database.export").</summary>
        [JsonPropertyName("entitlements")]
        public List<string> Entitlements { get; set; } = new List<string>();

        /// <summary>Arbitrary numeric quota and concurrency limits (e.g. "max_users" -> 10, "max_branches" -> 3).</summary>
        [JsonPropertyName("limits")]
        public Dictionary<string, long> Limits { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Extensible key-value metadata attributes (customer name, contract reference, etc.).</summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Identifier of the trusted authority signing key used to produce the digital signature.</summary>
        [JsonPropertyName("signing_key_id")]
        public string SigningKeyId { get; set; } = string.Empty;

        /// <summary>Base64-encoded asymmetric digital signature computed over the canonical representation of this certificate.</summary>
        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }
}
