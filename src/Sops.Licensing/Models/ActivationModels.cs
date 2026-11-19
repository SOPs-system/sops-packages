using System;
using System.Text.Json.Serialization;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Payload submitted when activating a license online or creating an offline activation request.
    /// </summary>
    public class ActivationRequest
    {
        /// <summary>Product identifier.</summary>
        [JsonPropertyName("product_id")]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Customer activation code or product key.</summary>
        [JsonPropertyName("activation_code")]
        public string ActivationCode { get; set; } = string.Empty;

        /// <summary>Stable installation identity.</summary>
        [JsonPropertyName("installation_id")]
        public string InstallationId { get; set; } = string.Empty;

        /// <summary>Hashed device fingerprint for hardware binding.</summary>
        [JsonPropertyName("device_fingerprint")]
        public string DeviceFingerprint { get; set; } = string.Empty;

        /// <summary>Client runtime platform (e.g. "Windows", "Linux", "macOS").</summary>
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        /// <summary>Client SDK version.</summary>
        [JsonPropertyName("client_version")]
        public string ClientVersion { get; set; } = "1.0.0";

        /// <summary>UTC timestamp when the request was initiated.</summary>
        [JsonPropertyName("timestamp_utc")]
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Random cryptographic nonce preventing replay attacks.</summary>
        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Payload returned by the licensing server or encoded in an offline activation file.
    /// </summary>
    public class ActivationResponse
    {
        /// <summary>Gets or sets whether the activation request succeeded.</summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>Raw JSON certificate text returned by the server, signed by the licensing authority.</summary>
        [JsonPropertyName("certificate_raw")]
        public string? CertificateRaw { get; set; }

        /// <summary>Diagnostic or status message from the server.</summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>Server-side error code if activation failed.</summary>
        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        /// <summary>Authoritative server UTC timestamp.</summary>
        [JsonPropertyName("server_timestamp_utc")]
        public DateTimeOffset ServerTimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Result of an activation attempt.
    /// </summary>
    public class LicenseActivationResult
    {
        /// <summary>Gets whether activation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets the diagnostic message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets the activated certificate if successful, or null.</summary>
        public LicenseCertificate? Certificate { get; set; }

        /// <summary>Gets the validation result of the newly activated license.</summary>
        public LicenseValidationResult? ValidationResult { get; set; }

        /// <summary>Creates a successful activation result.</summary>
        public static LicenseActivationResult Succeeded(LicenseCertificate cert, LicenseValidationResult validation)
        {
            return new LicenseActivationResult
            {
                Success = true,
                Message = "License activated successfully.",
                Certificate = cert,
                ValidationResult = validation
            };
        }

        /// <summary>Creates a failed activation result.</summary>
        public static LicenseActivationResult Failed(string message, LicenseValidationResult? validation = null)
        {
            return new LicenseActivationResult
            {
                Success = false,
                Message = message,
                ValidationResult = validation
            };
        }
    }

    /// <summary>
    /// Result of a license refresh attempt.
    /// </summary>
    public class LicenseRefreshResult
    {
        /// <summary>Gets whether the refresh succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Gets the status message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets the refreshed certificate, or null if refresh failed.</summary>
        public LicenseCertificate? Certificate { get; set; }
    }
}
