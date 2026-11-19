using System;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents a request to send a media message (image, document, audio, video, sticker) via WhatsApp.
    /// </summary>
    public class WhatsAppMediaMessageRequest
    {
        /// <summary>
        /// Gets or sets the recipient phone number.
        /// </summary>
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of media being sent.
        /// </summary>
        public WhatsAppMediaType MediaType { get; set; } = WhatsAppMediaType.Image;

        /// <summary>
        /// Gets or sets the Meta Graph API media ID, if already uploaded.
        /// </summary>
        public string? MediaId { get; set; }

        /// <summary>
        /// Gets or sets the publicly accessible HTTPS URL of the media, if hosted remotely.
        /// </summary>
        public string? Link { get; set; }

        /// <summary>
        /// Gets or sets the optional caption for image, document, or video messages.
        /// </summary>
        public string? Caption { get; set; }

        /// <summary>
        /// Gets or sets the optional filename displayed to recipient for document messages.
        /// </summary>
        public string? Filename { get; set; }

        /// <summary>
        /// Validates that required parameters are present and conform to basic constraints.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(To))
            {
                throw new ArgumentException("Recipient phone number ('To') cannot be null or whitespace.", nameof(To));
            }

            if (string.IsNullOrWhiteSpace(MediaId) && string.IsNullOrWhiteSpace(Link))
            {
                throw new ArgumentException("Either MediaId or Link must be provided to send a media message.", nameof(MediaId));
            }

            if (Link != null && !Uri.TryCreate(Link, UriKind.Absolute, out _))
            {
                throw new ArgumentException($"Media link '{Link}' must be a valid absolute URI.", nameof(Link));
            }
        }
    }
}
