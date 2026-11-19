using Sops.Payments.Abstractions;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents the outcome of a refund request.
    /// </summary>
    public sealed class RefundResult
    {
        /// <summary>
        /// Gets a value indicating whether the refund was processed successfully.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the gateway refund transaction identifier, if assigned.
        /// </summary>
        public string? RefundTransactionId { get; }

        /// <summary>
        /// Gets the original transaction identifier that was refunded.
        /// </summary>
        public string OriginalTransactionId { get; }

        /// <summary>
        /// Gets the monetary amount that was refunded.
        /// </summary>
        public Money? RefundedAmount { get; }

        /// <summary>
        /// Gets the status resulting from the refund operation.
        /// </summary>
        public PaymentStatus Status { get; }

        /// <summary>
        /// Gets the error classification if the refund failed.
        /// </summary>
        public PaymentErrorClassification ErrorClassification { get; }

        /// <summary>
        /// Gets an error message if the refund failed.
        /// </summary>
        public string? ErrorMessage { get; }

        private RefundResult(
            bool isSuccess,
            string? refundTransactionId,
            string originalTransactionId,
            Money? refundedAmount,
            PaymentStatus status,
            PaymentErrorClassification errorClassification,
            string? errorMessage)
        {
            IsSuccess = isSuccess;
            RefundTransactionId = refundTransactionId;
            OriginalTransactionId = originalTransactionId;
            RefundedAmount = refundedAmount;
            Status = status;
            ErrorClassification = errorClassification;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a successful refund result.
        /// </summary>
        public static RefundResult Success(string refundTransactionId, string originalTransactionId, Money refundedAmount)
        {
            return new RefundResult(
                isSuccess: true,
                refundTransactionId: refundTransactionId,
                originalTransactionId: originalTransactionId,
                refundedAmount: refundedAmount,
                status: PaymentStatus.Refunded,
                errorClassification: PaymentErrorClassification.None,
                errorMessage: null);
        }

        /// <summary>
        /// Creates a failed refund result.
        /// </summary>
        public static RefundResult Failure(
            string originalTransactionId,
            PaymentErrorClassification classification,
            string errorMessage)
        {
            return new RefundResult(
                isSuccess: false,
                refundTransactionId: null,
                originalTransactionId: originalTransactionId,
                refundedAmount: null,
                status: PaymentStatus.Failed,
                errorClassification: classification,
                errorMessage: errorMessage);
        }
    }
}
