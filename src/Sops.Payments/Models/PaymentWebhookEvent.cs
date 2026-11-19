using System;
using System.Collections.Generic;
using Sops.Payments.Abstractions;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents a verified server-to-server webhook callback event emitted by the payment provider.
    /// </summary>
    public sealed class PaymentWebhookEvent
    {
        /// <summary>
        /// Gets the event type (e.g. "TRANSACTION", "TOKEN", "ORDER").
        /// </summary>
        public string EventType { get; }

        /// <summary>
        /// Gets the gateway transaction identifier.
        /// </summary>
        public string TransactionId { get; }

        /// <summary>
        /// Gets the associated merchant or gateway order identifier.
        /// </summary>
        public string? OrderId { get; }

        /// <summary>
        /// Gets the normalized payment status determined from the webhook payload.
        /// </summary>
        public PaymentStatus Status { get; }

        /// <summary>
        /// Gets the transaction monetary amount.
        /// </summary>
        public Money Amount { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction is currently pending.
        /// </summary>
        public bool IsPending { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction was refunded.
        /// </summary>
        public bool IsRefunded { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction was voided.
        /// </summary>
        public bool IsVoided { get; }

        /// <summary>
        /// Gets the transaction timestamp.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the masked card PAN (e.g. "xxxx-xxxx-xxxx-1234") if applicable.
        /// </summary>
        public string? MaskedPan { get; }

        /// <summary>
        /// Gets the payment method or card brand (e.g. "Visa", "MasterCard", "Wallet").
        /// </summary>
        public string? PaymentMethod { get; }

        /// <summary>
        /// Gets the raw event payload dictionary for auditing or vendor-specific extra fields.
        /// </summary>
        public IReadOnlyDictionary<string, object?> RawPayload { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentWebhookEvent"/> class.
        /// </summary>
        public PaymentWebhookEvent(
            string eventType,
            string transactionId,
            string? orderId,
            PaymentStatus status,
            Money amount,
            bool isSuccess,
            bool isPending,
            bool isRefunded,
            bool isVoided,
            DateTimeOffset createdAt,
            string? maskedPan,
            string? paymentMethod,
            IReadOnlyDictionary<string, object?> rawPayload)
        {
            EventType = eventType;
            TransactionId = transactionId;
            OrderId = orderId;
            Status = status;
            Amount = amount;
            IsSuccess = isSuccess;
            IsPending = isPending;
            IsRefunded = isRefunded;
            IsVoided = isVoided;
            CreatedAt = createdAt;
            MaskedPan = maskedPan;
            PaymentMethod = paymentMethod;
            RawPayload = rawPayload;
        }
    }
}
