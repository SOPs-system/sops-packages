using Sops.WhatsApp.Abstractions;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents the result of an operation to send a WhatsApp message.
    /// </summary>
    public class WhatsAppMessageResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the message was accepted by Meta Cloud API.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the unique WhatsApp message identifier (e.g. "wamid.HBgLM...").
        /// </summary>
        public string? MessageId { get; set; }

        /// <summary>
        /// Gets or sets the initial delivery status of the message.
        /// </summary>
        public WhatsAppDeliveryStatus Status { get; set; } = WhatsAppDeliveryStatus.Unknown;

        /// <summary>
        /// Gets or sets the recipient's WhatsApp ID assigned by Meta.
        /// </summary>
        public string? RecipientWaId { get; set; }

        /// <summary>
        /// Gets or sets the classification of any error encountered.
        /// </summary>
        public WhatsAppErrorClassification ErrorClassification { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the upstream numeric error code returned by Meta Graph API, if available.
        /// </summary>
        public int? ErrorCode { get; set; }

        /// <summary>
        /// Creates a successful send result.
        /// </summary>
        /// <param name="messageId">The Meta WhatsApp message ID.</param>
        /// <param name="recipientWaId">The recipient WhatsApp ID.</param>
        /// <returns>A successful <see cref="WhatsAppMessageResult"/>.</returns>
        public static WhatsAppMessageResult Success(string messageId, string? recipientWaId = null)
        {
            return new WhatsAppMessageResult
            {
                IsSuccess = true,
                MessageId = messageId,
                RecipientWaId = recipientWaId,
                Status = WhatsAppDeliveryStatus.Sent,
                ErrorClassification = WhatsAppErrorClassification.None
            };
        }

        /// <summary>
        /// Creates a failed send result.
        /// </summary>
        /// <param name="errorMessage">Description of the failure.</param>
        /// <param name="classification">Classification of the error.</param>
        /// <param name="errorCode">Optional upstream error code.</param>
        /// <returns>A failed <see cref="WhatsAppMessageResult"/>.</returns>
        public static WhatsAppMessageResult Failure(string errorMessage, WhatsAppErrorClassification classification = WhatsAppErrorClassification.Permanent, int? errorCode = null)
        {
            return new WhatsAppMessageResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorClassification = classification,
                ErrorCode = errorCode,
                Status = WhatsAppDeliveryStatus.Failed
            };
        }
    }
}
