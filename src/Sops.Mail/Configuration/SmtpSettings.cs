using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Models;

namespace Sops.Mail.Configuration
{
    /// <summary>
    /// Configuration settings for SMTP mail transmission.
    /// </summary>
    public class SmtpSettings : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the SMTP server hostname or IP address.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the SMTP port. Defaults to 587.
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Gets or sets the security mode for SMTP transmission. Defaults to <see cref="SmtpSecurity.Auto"/>.
        /// </summary>
        public SmtpSecurity Security { get; set; } = SmtpSecurity.Auto;

        /// <summary>
        /// Gets or sets the username for SMTP authentication, if required.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the password for SMTP authentication, if required.
        /// Marked as sensitive to prevent exposure in logs, traces, or diagnostic dumps.
        /// </summary>
        [Sensitive]
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the connection and operation timeout in seconds. Defaults to 30.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether authentication is explicitly required.
        /// If not set, authentication is attempted when <see cref="Username"/> is non-empty.
        /// </summary>
        public bool RequireAuthentication { get; set; }

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                yield return new ValidationResult(
                    "SMTP host cannot be null or whitespace.",
                    new[] { nameof(Host) });
            }

            if (Port <= 0 || Port > 65535)
            {
                yield return new ValidationResult(
                    $"SMTP port must be between 1 and 65535. Current value: {Port}.",
                    new[] { nameof(Port) });
            }

            if (TimeoutSeconds <= 0)
            {
                yield return new ValidationResult(
                    $"Timeout seconds must be greater than zero. Current value: {TimeoutSeconds}.",
                    new[] { nameof(TimeoutSeconds) });
            }

            if (RequireAuthentication && string.IsNullOrWhiteSpace(Username))
            {
                yield return new ValidationResult(
                    "SMTP authentication is required, but no username was provided.",
                    new[] { nameof(Username) });
            }
        }
    }
}
