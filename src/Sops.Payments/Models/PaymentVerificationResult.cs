using System;
using Sops.Payments.Abstractions;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents server-side verification details for a payment transaction queried directly from the gateway.
    /// </summary>
    public sealed class PaymentVerificationResult
    {
        /// <summary>
        /// Gets a value indicating whether the transaction was conclusively verified as successful.
        /// </summary>
        public bool IsSuccess => Status == PaymentStatus.Successful;

        /// <summary>
        /// Gets the verified payment lifecycle status.
        /// </summary>
        public PaymentStatus Status { get; }

        /// <summary>
        /// Gets the gateway transaction identifier.
        /// </summary>
        public string TransactionId { get; }

        /// <summary>
        /// Gets the merchant or gateway order identifier.
        /// </summary>
        public string? OrderId { get; }

        /// <summary>
        /// Gets the verified monetary amount.
        /// </summary>
        public Money Amount { get; }

        /// <summary>
        /// Gets the gateway transaction timestamp, if available.
        /// </summary>
        public DateTimeOffset? TransactionDate { get; }

        /// <summary>
        /// Gets the payment method used (e.g. "card", "wallet", "kiosk").
        /// </summary>
        public string? PaymentMethod { get; }

        /// <summary>
        /// Gets the masked card PAN (e.g. "xxxx-xxxx-xxxx-1234") if paid by card. Never contains full PAN.
        /// </summary>
        public string? MaskedPan { get; }

        /// <summary>
        /// Gets the error description if the payment failed or could not be verified.
        /// </summary>
        public string? ErrorMessage { get; }

        private PaymentVerificationResult(
            PaymentStatus status,
            string transactionId,
            string? orderId,
            Money amount,
            DateTimeOffset? transactionDate,
            string? paymentMethod,
            string? maskedPan,
            string? errorMessage)
        {
            Status = status;
            TransactionId = transactionId;
            OrderId = orderId;
            Amount = amount;
            TransactionDate = transactionDate;
            PaymentMethod = paymentMethod;
            MaskedPan = maskedPan;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a verification result for a confirmed transaction.
        /// </summary>
        public static PaymentVerificationResult Success(
            string transactionId,
            string? orderId,
            Money amount,
            DateTimeOffset? transactionDate = null,
            string? paymentMethod = null,
            string? maskedPan = null)
        {
            return new PaymentVerificationResult(
                status: PaymentStatus.Successful,
                transactionId: transactionId,
                orderId: orderId,
                amount: amount,
                transactionDate: transactionDate,
                paymentMethod: paymentMethod,
                maskedPan: maskedPan,
                errorMessage: null);
        }

        /// <summary>
        /// Creates a verification result for a pending, failed, or cancelled transaction.
        /// </summary>
        public static PaymentVerificationResult NonSuccess(
            PaymentStatus status,
            string transactionId,
            string? orderId,
            Money amount,
            string? errorMessage = null,
            DateTimeOffset? transactionDate = null,
            string? paymentMethod = null,
            string? maskedPan = null)
        {
            return new PaymentVerificationResult(
                status: status,
                transactionId: transactionId,
                orderId: orderId,
                amount: amount,
                transactionDate: transactionDate,
                paymentMethod: paymentMethod,
                maskedPan: maskedPan,
                errorMessage: errorMessage);
        }
    }
}
