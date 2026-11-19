using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sops.Payments.Configuration
{
    /// <summary>
    /// Top-level configuration settings for payment processing in SOPs applications.
    /// </summary>
    public class PaymentSettings : IValidatableObject
    {
        /// <summary>
        /// The default configuration section name.
        /// </summary>
        public const string SectionName = "Payment";

        /// <summary>
        /// Gets or sets the active payment provider identifier (e.g. "Paymob", "Stripe"). Defaults to "Paymob".
        /// </summary>
        public string Provider { get; set; } = "Paymob";

        /// <summary>
        /// Gets or sets a value indicating whether payment operations are enabled. Defaults to true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets configuration settings specific to the Paymob gateway provider.
        /// </summary>
        public PaymobSettings Paymob { get; set; } = new PaymobSettings();

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                yield return new ValidationResult(
                    "Payment provider cannot be null or whitespace.",
                    new[] { nameof(Provider) });
            }

            if (string.Equals(Provider, "Paymob", StringComparison.OrdinalIgnoreCase))
            {
                if (Paymob == null)
                {
                    yield return new ValidationResult(
                        "Paymob settings must be provided when provider is 'Paymob'.",
                        new[] { nameof(Paymob) });
                }
                else
                {
                    var paymobValidation = Paymob.Validate(validationContext);
                    foreach (var result in paymobValidation)
                    {
                        yield return result;
                    }
                }
            }
        }
    }
}
