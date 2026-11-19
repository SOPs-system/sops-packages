using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Sops.Mail.Configuration
{
    /// <summary>
    /// Top-level configuration settings for SOPs email services.
    /// </summary>
    public class MailSettings : IValidatableObject
    {
        /// <summary>
        /// The default configuration section name.
        /// </summary>
        public const string SectionName = "Mail";

        /// <summary>
        /// Gets or sets the mail provider type identifier. Defaults to "Smtp".
        /// </summary>
        public string Provider { get; set; } = "Smtp";

        /// <summary>
        /// Gets or sets a value indicating whether email sending is enabled.
        /// When disabled, messages are dropped or routed to a no-op sink. Defaults to true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default sender email address.
        /// </summary>
        public string? FromAddress { get; set; }

        /// <summary>
        /// Gets or sets the default sender display name.
        /// </summary>
        public string? FromName { get; set; }

        /// <summary>
        /// Gets or sets SMTP-specific connection settings.
        /// </summary>
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                yield return new ValidationResult(
                    "Mail provider cannot be null or whitespace.",
                    new[] { nameof(Provider) });
            }

            if (!string.IsNullOrWhiteSpace(FromAddress))
            {
                bool valid = false;
                try
                {
                    _ = new MailAddress(FromAddress);
                    valid = true;
                }
                catch
                {
                    valid = false;
                }

                if (!valid)
                {
                    yield return new ValidationResult(
                        $"FromAddress '{FromAddress}' is not a valid email address.",
                        new[] { nameof(FromAddress) });
                }
            }

            if (string.Equals(Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
            {
                if (Smtp == null)
                {
                    yield return new ValidationResult(
                        "SMTP settings must be provided when provider is 'Smtp'.",
                        new[] { nameof(Smtp) });
                }
                else
                {
                    var smtpValidation = Smtp.Validate(validationContext);
                    foreach (var result in smtpValidation)
                    {
                        yield return result;
                    }
                }
            }
        }
    }
}
