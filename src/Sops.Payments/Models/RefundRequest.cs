using System;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents a request to refund a previously captured payment transaction.
    /// </summary>
    public sealed class RefundRequest
    {
        /// <summary>
        /// Gets or sets the original gateway transaction identifier to refund.
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the monetary amount to refund.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// Gets or sets an optional reason or explanation for the refund.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets an optional caller-generated idempotency key to prevent duplicate refunds.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="RefundRequest"/> class.
        /// </summary>
        public RefundRequest()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefundRequest"/> class.
        /// </summary>
        /// <param name="transactionId">Original transaction identifier.</param>
        /// <param name="amount">Monetary amount to refund.</param>
        /// <param name="reason">Optional reason.</param>
        /// <param name="idempotencyKey">Optional idempotency key.</param>
        public RefundRequest(string transactionId, Money amount, string? reason = null, string? idempotencyKey = null)
        {
            TransactionId = transactionId;
            Amount = amount;
            Reason = reason;
            IdempotencyKey = idempotencyKey;
        }

        /// <summary>
        /// Validates that the refund request contains valid identifiers and amounts.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TransactionId))
            {
                throw new InvalidOperationException("Original transaction ID is required for a refund.");
            }

            if (Amount.Cents <= 0)
            {
                throw new InvalidOperationException("Refund amount must be greater than zero.");
            }
        }
    }
}
