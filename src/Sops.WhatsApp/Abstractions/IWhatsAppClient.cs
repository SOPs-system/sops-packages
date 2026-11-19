using System.Threading;
using System.Threading.Tasks;
using Sops.WhatsApp.Models;

namespace Sops.WhatsApp.Abstractions
{
    /// <summary>
    /// Contract for interacting with the WhatsApp Business Platform Cloud API.
    /// Supports sending text, templates, and media messages, uploading and querying media, and validating/processing webhooks.
    /// </summary>
    public interface IWhatsAppClient
    {
        /// <summary>
        /// Gets the name of the WhatsApp service provider (e.g. "MetaCloud").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Sends a plain text message to a recipient.
        /// </summary>
        /// <param name="request">The text message parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result containing delivery status or failure details.</returns>
        Task<WhatsAppMessageResult> SendTextMessageAsync(WhatsAppTextMessageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a pre-approved template message (including marketing, utility, or authentication/OTP templates).
        /// </summary>
        /// <param name="request">The template message parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result containing delivery status or failure details.</returns>
        Task<WhatsAppMessageResult> SendTemplateAsync(WhatsAppTemplateMessageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an image, document, audio, video, or sticker message to a recipient.
        /// </summary>
        /// <param name="request">The media message parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result containing delivery status or failure details.</returns>
        Task<WhatsAppMessageResult> SendMediaAsync(WhatsAppMediaMessageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a media file (image, document, audio, video, sticker) to Meta WhatsApp servers.
        /// </summary>
        /// <param name="request">The file upload parameters containing stream or bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome of the upload containing the newly generated Meta media ID.</returns>
        Task<WhatsAppMediaUploadResult> UploadMediaAsync(WhatsAppMediaUploadRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves metadata (URL, MIME type, SHA256, file size) for an uploaded Meta media asset.
        /// </summary>
        /// <param name="mediaId">The Meta media identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Media metadata if found; otherwise, <c>null</c>.</returns>
        Task<WhatsAppMediaMetadata?> GetMediaMetadataAsync(string mediaId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a webhook subscription challenge received during Meta Webhook setup (HTTP GET).
        /// </summary>
        /// <param name="mode">The hub.mode query parameter.</param>
        /// <param name="token">The hub.verify_token query parameter.</param>
        /// <param name="challenge">The hub.challenge query parameter.</param>
        /// <param name="response">Outputs the challenge string to echo back with HTTP 200 if valid.</param>
        /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
        bool VerifyWebhookChallenge(string? mode, string? token, string? challenge, out string? response);

        /// <summary>
        /// Validates the HMAC-SHA256 signature of an inbound webhook notification (HTTP POST).
        /// </summary>
        /// <param name="signatureHeader">The X-Hub-Signature-256 header value.</param>
        /// <param name="rawPayload">The raw UTF-8 JSON payload body.</param>
        /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
        bool ValidateWebhookSignature(string? signatureHeader, string rawPayload);

        /// <summary>
        /// Parses and extracts structured events (messages, status updates) from an inbound webhook JSON payload.
        /// </summary>
        /// <param name="rawPayload">The raw UTF-8 JSON payload body.</param>
        /// <returns>A structured <see cref="WhatsAppWebhookEvent"/> representation.</returns>
        WhatsAppWebhookEvent ProcessWebhook(string rawPayload);
    }
}
