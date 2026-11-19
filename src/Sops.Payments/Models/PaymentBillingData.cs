namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents billing and geographical address information required by payment gateways.
    /// </summary>
    public sealed class PaymentBillingData
    {
        /// <summary>
        /// Gets or sets the billing first name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the billing last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the billing email.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the billing phone number.
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the apartment number or identifier. Defaults to "NA".
        /// </summary>
        public string Apartment { get; set; } = "NA";

        /// <summary>
        /// Gets or sets the floor number or identifier. Defaults to "NA".
        /// </summary>
        public string Floor { get; set; } = "NA";

        /// <summary>
        /// Gets or sets the street address. Defaults to "NA".
        /// </summary>
        public string Street { get; set; } = "NA";

        /// <summary>
        /// Gets or sets the building number or name. Defaults to "NA".
        /// </summary>
        public string Building { get; set; } = "NA";

        /// <summary>
        /// Gets or sets the city. Defaults to "Cairo".
        /// </summary>
        public string City { get; set; } = "Cairo";

        /// <summary>
        /// Gets or sets the state or governorate. Defaults to "Cairo".
        /// </summary>
        public string State { get; set; } = "Cairo";

        /// <summary>
        /// Gets or sets the country code. Defaults to "EG".
        /// </summary>
        public string Country { get; set; } = "EG";

        /// <summary>
        /// Gets or sets the postal or ZIP code. Defaults to "NA".
        /// </summary>
        public string PostalCode { get; set; } = "NA";

        /// <summary>
        /// Creates a <see cref="PaymentBillingData"/> instance populated from a <see cref="PaymentCustomer"/>.
        /// </summary>
        /// <param name="customer">The source customer details.</param>
        /// <returns>A billing data instance with customer information and standard fallback address values.</returns>
        public static PaymentBillingData FromCustomer(PaymentCustomer customer)
        {
            if (customer == null) return new PaymentBillingData();

            return new PaymentBillingData
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber
            };
        }
    }
}
