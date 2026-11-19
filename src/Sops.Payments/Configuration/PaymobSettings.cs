using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Models;

namespace Sops.Payments.Configuration
{
    /// <summary>
    /// Configuration settings for Paymob payment gateway integration.
    /// Supports both the modern Unified Checkout (Intention API) and the legacy iframe flow.
    /// </summary>
    public class PaymobSettings : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the base URL for Paymob API endpoints. Defaults to "https://accept.paymob.com".
        /// </summary>
        public string BaseUrl { get; set; } = "https://accept.paymob.com";

        /// <summary>
        /// Gets or sets the Paymob API Secret Key (used for the modern Intention API).
        /// Marked as sensitive to prevent exposure in logs, traces, or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? SecretKey { get; set; }

        /// <summary>
        /// Gets or sets the Paymob Public Key (used to construct the Unified Checkout redirection URL).
        /// </summary>
        public string? PublicKey { get; set; }

        /// <summary>
        /// Gets or sets the Paymob API Key (used for legacy authentication token requests).
        /// Marked as sensitive to prevent exposure in logs, traces, or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the Paymob HMAC Secret (configured in the Paymob Dashboard for SHA-512 webhook verification).
        /// Marked as sensitive to prevent exposure in logs, traces, or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? HmacSecret { get; set; }

        /// <summary>
        /// Gets or sets the primary payment integration identifier (e.g. Card Integration ID).
        /// </summary>
        public int IntegrationId { get; set; }

        /// <summary>
        /// Gets or sets the optional iframe identifier for legacy iframe displays.
        /// </summary>
        public int? IframeId { get; set; }

        /// <summary>
        /// Gets or sets the HTTP request timeout in seconds. Defaults to 30.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default three-letter ISO currency code. Defaults to "EGP".
        /// </summary>
        public string DefaultCurrency { get; set; } = "EGP";

        /// <summary>
        /// Gets or sets the optional default server-to-server webhook notification callback URL.
        /// </summary>
        public string? NotificationUrl { get; set; }

        /// <summary>
        /// Gets or sets the optional default customer browser redirection URL upon payment completion.
        /// </summary>
        public string? RedirectionUrl { get; set; }

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                yield return new ValidationResult(
                    "Paymob BaseUrl cannot be null or whitespace.",
                    new[] { nameof(BaseUrl) });
            }
            else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult(
                    $"Paymob BaseUrl '{BaseUrl}' is not a valid HTTP or HTTPS URL.",
                    new[] { nameof(BaseUrl) });
            }

            if (string.IsNullOrWhiteSpace(SecretKey) && string.IsNullOrWhiteSpace(ApiKey))
            {
                yield return new ValidationResult(
                    "Either SecretKey (for modern Unified Checkout) or ApiKey (for legacy flow) must be provided for Paymob.",
                    new[] { nameof(SecretKey), nameof(ApiKey) });
            }

            if (TimeoutSeconds <= 0)
            {
                yield return new ValidationResult(
                    $"Timeout seconds must be greater than zero. Current value: {TimeoutSeconds}.",
                    new[] { nameof(TimeoutSeconds) });
            }

            if (string.IsNullOrWhiteSpace(DefaultCurrency) || DefaultCurrency.Trim().Length != 3)
            {
                yield return new ValidationResult(
                    $"DefaultCurrency '{DefaultCurrency}' must be a valid 3-letter ISO code.",
                    new[] { nameof(DefaultCurrency) });
            }
        }
    }
}
