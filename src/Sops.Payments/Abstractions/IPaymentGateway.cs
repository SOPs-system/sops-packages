using System.Threading;
using System.Threading.Tasks;
using Sops.Payments.Models;

namespace Sops.Payments.Abstractions
{
    /// <summary>
    /// Core abstraction for payment gateway operations, including payment creation, verification, refunds, and webhook processing.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// Initiates or registers a new payment transaction with the gateway.
        /// </summary>
        /// <param name="request">The payment request details.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="PaymentResult"/> containing checkout URLs, client secrets, or initial transaction IDs.</returns>
        Task<PaymentResult> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs authoritative server-side verification of a transaction directly against the payment provider.
        /// Does not rely on client-side redirects or untrusted browser payloads.
        /// </summary>
        /// <param name="transactionId">The gateway transaction identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="PaymentVerificationResult"/> containing verified state, amount, and payment details.</returns>
        Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues a refund for a previously captured transaction.
        /// </summary>
        /// <param name="request">The refund request parameters.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="RefundResult"/> detailing the refund outcome.</returns>
        Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates cryptographic webhook signatures and parses the incoming webhook notification into a strongly typed event.
        /// </summary>
        /// <param name="rawPayload">The raw JSON payload received from the gateway webhook.</param>
        /// <param name="signature">The webhook signature or HMAC provided by the gateway in query or headers.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A verified <see cref="PaymentWebhookEvent"/>, or null if the signature is invalid or payload malformed.</returns>
        Task<PaymentWebhookEvent?> ProcessWebhookAsync(string rawPayload, string? signature, CancellationToken cancellationToken = default);
    }
}
