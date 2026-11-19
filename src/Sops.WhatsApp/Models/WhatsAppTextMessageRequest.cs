using System;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents a request to send a plain text message via WhatsApp.
    /// </summary>
    public class WhatsAppTextMessageRequest
    {
        /// <summary>
        /// Gets or sets the recipient phone number in E.164 or international digit format.
        /// </summary>
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the text content of the message.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether URL previews should be generated in the message.
        /// </summary>
        public bool PreviewUrl { get; set; }

        /// <summary>
        /// Validates that required parameters are present and conform to basic constraints.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(To))
            {
                throw new ArgumentException("Recipient phone number ('To') cannot be null or whitespace.", nameof(To));
            }

            if (string.IsNullOrWhiteSpace(Body))
            {
                throw new ArgumentException("Message body cannot be null or whitespace.", nameof(Body));
            }

            if (Body.Length > 4096)
            {
                throw new ArgumentException("Message body exceeds the maximum permitted length of 4096 characters.", nameof(Body));
            }
        }
    }
}
