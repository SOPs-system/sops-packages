using Sops.WhatsApp.Abstractions;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents the outcome of an attempt to upload a media file to Meta's WhatsApp servers.
    /// </summary>
    public class WhatsAppMediaUploadResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the media upload succeeded.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the Meta Graph API media ID generated for the uploaded asset.
        /// </summary>
        public string? MediaId { get; set; }

        /// <summary>
        /// Gets or sets the error classification if the upload failed.
        /// </summary>
        public WhatsAppErrorClassification ErrorClassification { get; set; }

        /// <summary>
        /// Gets or sets the error message if the upload failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Creates a successful upload result.
        /// </summary>
        /// <param name="mediaId">The returned Meta media ID.</param>
        /// <returns>A successful <see cref="WhatsAppMediaUploadResult"/>.</returns>
        public static WhatsAppMediaUploadResult Success(string mediaId)
        {
            return new WhatsAppMediaUploadResult
            {
                IsSuccess = true,
                MediaId = mediaId,
                ErrorClassification = WhatsAppErrorClassification.None
            };
        }

        /// <summary>
        /// Creates a failed upload result.
        /// </summary>
        /// <param name="errorMessage">The error description.</param>
        /// <param name="classification">The error classification.</param>
        /// <returns>A failed <see cref="WhatsAppMediaUploadResult"/>.</returns>
        public static WhatsAppMediaUploadResult Failure(string errorMessage, WhatsAppErrorClassification classification = WhatsAppErrorClassification.Permanent)
        {
            return new WhatsAppMediaUploadResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                ErrorClassification = classification
            };
        }
    }
}
