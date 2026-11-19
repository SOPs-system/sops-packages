using Sops.Payments.Abstractions;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents the outcome of a payment creation or initiation attempt.
    /// </summary>
    public sealed class PaymentResult
    {
        /// <summary>
        /// Gets a value indicating whether the initiation or capture succeeded.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the current lifecycle status of the payment.
        /// </summary>
        public PaymentStatus Status { get; }

        /// <summary>
        /// Gets the gateway transaction identifier, if assigned.
        /// </summary>
        public string? TransactionId { get; }

        /// <summary>
        /// Gets the associated merchant or gateway order identifier.
        /// </summary>
        public string? OrderId { get; }

        /// <summary>
        /// Gets the customer checkout URL for web redirection or iframe display.
        /// </summary>
        public string? CheckoutUrl { get; }

        /// <summary>
        /// Gets the client secret or token (e.g. Paymob intention client_secret) for frontend SDKs.
        /// </summary>
        public string? ClientSecret { get; }

        /// <summary>
        /// Gets the error classification if the operation failed.
        /// </summary>
        public PaymentErrorClassification ErrorClassification { get; }

        /// <summary>
        /// Gets the provider-specific error code, if available.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Gets a human-readable description of any error encountered.
        /// </summary>
        public string? ErrorMessage { get; }

        private PaymentResult(
            bool isSuccess,
            PaymentStatus status,
            string? transactionId,
            string? orderId,
            string? checkoutUrl,
            string? clientSecret,
            PaymentErrorClassification errorClassification,
            string? errorCode,
            string? errorMessage)
        {
            IsSuccess = isSuccess;
            Status = status;
            TransactionId = transactionId;
            OrderId = orderId;
            CheckoutUrl = checkoutUrl;
            ClientSecret = clientSecret;
            ErrorClassification = errorClassification;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a result representing a successfully initiated payment awaiting customer completion.
        /// </summary>
        /// <param name="checkoutUrl">The checkout redirection URL.</param>
        /// <param name="clientSecret">The client secret or intention token.</param>
        /// <param name="orderId">The associated order identifier.</param>
        /// <param name="transactionId">Optional initial transaction identifier.</param>
        /// <returns>A pending <see cref="PaymentResult"/>.</returns>
        public static PaymentResult Pending(
            string? checkoutUrl,
            string? clientSecret = null,
            string? orderId = null,
            string? transactionId = null)
        {
            return new PaymentResult(
                isSuccess: true,
                status: PaymentStatus.Pending,
                transactionId: transactionId,
                orderId: orderId,
                checkoutUrl: checkoutUrl,
                clientSecret: clientSecret,
                errorClassification: PaymentErrorClassification.None,
                errorCode: null,
                errorMessage: null);
        }

        /// <summary>
        /// Creates a result representing a completed or immediately captured payment.
        /// </summary>
        /// <param name="transactionId">The gateway transaction identifier.</param>
        /// <param name="orderId">The order identifier.</param>
        /// <returns>A successful <see cref="PaymentResult"/>.</returns>
        public static PaymentResult Success(string transactionId, string? orderId = null)
        {
            return new PaymentResult(
                isSuccess: true,
                status: PaymentStatus.Successful,
                transactionId: transactionId,
                orderId: orderId,
                checkoutUrl: null,
                clientSecret: null,
                errorClassification: PaymentErrorClassification.None,
                errorCode: null,
                errorMessage: null);
        }

        /// <summary>
        /// Creates a result representing a failed payment operation.
        /// </summary>
        /// <param name="classification">The error classification.</param>
        /// <param name="errorMessage">The error description.</param>
        /// <param name="errorCode">Optional provider error code.</param>
        /// <param name="status">Optional status code (defaults to Failed).</param>
        /// <returns>A failed <see cref="PaymentResult"/>.</returns>
        public static PaymentResult Failure(
            PaymentErrorClassification classification,
            string errorMessage,
            string? errorCode = null,
            PaymentStatus status = PaymentStatus.Failed)
        {
            return new PaymentResult(
                isSuccess: false,
                status: status,
                transactionId: null,
                orderId: null,
                checkoutUrl: null,
                clientSecret: null,
                errorClassification: classification,
                errorCode: errorCode,
                errorMessage: errorMessage);
        }
    }
}
