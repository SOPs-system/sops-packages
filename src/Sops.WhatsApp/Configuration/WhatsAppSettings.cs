using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Models;

namespace Sops.WhatsApp.Configuration
{
    /// <summary>
    /// Configuration settings for WhatsApp Business Platform Cloud API integration.
    /// </summary>
    public class WhatsAppSettings : IValidatableObject
    {
        /// <summary>
        /// The default configuration section name in application settings.
        /// </summary>
        public const string SectionName = "WhatsApp";

        /// <summary>
        /// Gets or sets the WhatsApp provider identifier. Defaults to "MetaCloud".
        /// </summary>
        public string Provider { get; set; } = "MetaCloud";

        /// <summary>
        /// Gets or sets a value indicating whether WhatsApp messaging operations are enabled. Defaults to true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the Meta Graph API base URL. Defaults to "https://graph.facebook.com".
        /// </summary>
        public string BaseUrl { get; set; } = "https://graph.facebook.com";

        /// <summary>
        /// Gets or sets the Meta Graph API version. Defaults to "v21.0".
        /// </summary>
        public string GraphApiVersion { get; set; } = "v21.0";

        /// <summary>
        /// Gets or sets the WhatsApp Business Phone Number ID registered with Meta.
        /// </summary>
        public string? PhoneNumberId { get; set; }

        /// <summary>
        /// Gets or sets the optional WhatsApp Business Account ID (WABA ID).
        /// </summary>
        public string? BusinessAccountId { get; set; }

        /// <summary>
        /// Gets or sets the Meta Cloud API system user access token with whatsapp_business_messaging permission.
        /// Marked as sensitive to prevent exposure in logs or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the Meta App Secret used to compute and verify HMAC-SHA256 signatures for webhook events.
        /// Marked as sensitive to prevent exposure in logs or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? AppSecret { get; set; }

        /// <summary>
        /// Gets or sets the shared webhook verification token configured in the Meta App Dashboard for GET challenge verification.
        /// Marked as sensitive to prevent exposure in logs or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? WebhookVerifyToken { get; set; }

        /// <summary>
        /// Gets or sets the HTTP request timeout in seconds. Defaults to 30.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the default language/locale code for template messages. Defaults to "en_US".
        /// </summary>
        public string DefaultLanguage { get; set; } = "en_US";

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                yield return new ValidationResult(
                    "WhatsApp provider cannot be null or whitespace.",
                    new[] { nameof(Provider) });
            }

            if (!Enabled)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                yield return new ValidationResult(
                    "BaseUrl cannot be null or whitespace when WhatsApp is enabled.",
                    new[] { nameof(BaseUrl) });
            }
            else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult(
                    $"BaseUrl '{BaseUrl}' is not a valid HTTP or HTTPS absolute URL.",
                    new[] { nameof(BaseUrl) });
            }

            if (string.IsNullOrWhiteSpace(GraphApiVersion))
            {
                yield return new ValidationResult(
                    "GraphApiVersion cannot be null or whitespace when WhatsApp is enabled.",
                    new[] { nameof(GraphApiVersion) });
            }

            if (string.IsNullOrWhiteSpace(PhoneNumberId))
            {
                yield return new ValidationResult(
                    "PhoneNumberId is required when WhatsApp is enabled.",
                    new[] { nameof(PhoneNumberId) });
            }

            if (string.IsNullOrWhiteSpace(AccessToken))
            {
                yield return new ValidationResult(
                    "AccessToken is required when WhatsApp is enabled.",
                    new[] { nameof(AccessToken) });
            }

            if (TimeoutSeconds <= 0 || TimeoutSeconds > 300)
            {
                yield return new ValidationResult(
                    $"TimeoutSeconds must be between 1 and 300. Provided value was {TimeoutSeconds}.",
                    new[] { nameof(TimeoutSeconds) });
            }

            if (string.IsNullOrWhiteSpace(DefaultLanguage))
            {
                yield return new ValidationResult(
                    "DefaultLanguage cannot be null or whitespace.",
                    new[] { nameof(DefaultLanguage) });
            }
        }
    }
}
