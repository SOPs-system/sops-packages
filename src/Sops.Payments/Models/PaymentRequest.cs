using System;
using System.Collections.Generic;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents a request to create or initiate a payment.
    /// </summary>
    public sealed class PaymentRequest
    {
        /// <summary>
        /// Gets or sets the monetary amount to charge.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// Gets or sets the unique merchant order identifier.
        /// </summary>
        public string? OrderId { get; set; }

        /// <summary>
        /// Gets or sets the caller-generated idempotency key.
        /// Reusing the same key on transient retry requests prevents duplicate charges.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// Gets or sets the customer purchasing this order.
        /// </summary>
        public PaymentCustomer Customer { get; set; } = new PaymentCustomer();

        /// <summary>
        /// Gets or sets the customer's billing address information.
        /// If omitted, it will be automatically derived from <see cref="Customer"/>.
        /// </summary>
        public PaymentBillingData? BillingData { get; set; }

        /// <summary>
        /// Gets the list of items being purchased.
        /// </summary>
        public IList<PaymentItem> Items { get; } = new List<PaymentItem>();

        /// <summary>
        /// Gets or sets the server-to-server webhook notification URL.
        /// </summary>
        public string? NotificationUrl { get; set; }

        /// <summary>
        /// Gets or sets the browser redirection URL after payment completion.
        /// </summary>
        public string? RedirectionUrl { get; set; }

        /// <summary>
        /// Gets custom metadata dictionary for storing application context.
        /// </summary>
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new empty instance of the <see cref="PaymentRequest"/> class.
        /// </summary>
        public PaymentRequest()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentRequest"/> class.
        /// </summary>
        /// <param name="amount">The payment amount.</param>
        /// <param name="customer">The customer details.</param>
        /// <param name="orderId">Optional merchant order identifier.</param>
        /// <param name="idempotencyKey">Optional idempotency key.</param>
        public PaymentRequest(Money amount, PaymentCustomer customer, string? orderId = null, string? idempotencyKey = null)
        {
            Amount = amount;
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            OrderId = orderId;
            IdempotencyKey = idempotencyKey;
        }

        /// <summary>
        /// Validates that the request has required information and non-zero amount.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public void Validate()
        {
            if (Amount.Cents <= 0)
            {
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(Amount.Currency))
            {
                throw new InvalidOperationException("Payment currency code must be specified.");
            }

            if (Customer == null)
            {
                throw new InvalidOperationException("Customer details must be specified.");
            }

            if (string.IsNullOrWhiteSpace(Customer.Email))
            {
                throw new InvalidOperationException("Customer email address is required.");
            }

            if (string.IsNullOrWhiteSpace(Customer.PhoneNumber))
            {
                throw new InvalidOperationException("Customer phone number is required.");
            }
        }
    }
}
