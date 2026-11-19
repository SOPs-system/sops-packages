using System;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents customer identity details for payment processing.
    /// </summary>
    public sealed class PaymentCustomer
    {
        /// <summary>
        /// Gets or sets the customer's first name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the customer's last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the customer's email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the customer's contact telephone number.
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new empty instance of the <see cref="PaymentCustomer"/> class.
        /// </summary>
        public PaymentCustomer()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentCustomer"/> class with required details.
        /// </summary>
        /// <param name="firstName">Customer first name.</param>
        /// <param name="lastName">Customer last name.</param>
        /// <param name="email">Customer email address.</param>
        /// <param name="phoneNumber">Customer phone number.</param>
        public PaymentCustomer(string firstName, string lastName, string email, string phoneNumber)
        {
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            Email = email ?? string.Empty;
            PhoneNumber = phoneNumber ?? string.Empty;
        }
    }
}
