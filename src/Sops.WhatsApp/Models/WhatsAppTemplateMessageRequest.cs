using System;
using System.Collections.Generic;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents a request to send a pre-approved template message via WhatsApp.
    /// </summary>
    public class WhatsAppTemplateMessageRequest
    {
        /// <summary>
        /// Gets or sets the recipient phone number.
        /// </summary>
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the pre-approved template.
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ISO language/locale code for the template (e.g. "en_US", "ar", "es_ES").
        /// </summary>
        public string LanguageCode { get; set; } = "en_US";

        /// <summary>
        /// Gets or sets the list of template components (header, body, button parameters).
        /// </summary>
        public List<WhatsAppTemplateComponent> Components { get; set; } = new List<WhatsAppTemplateComponent>();

        /// <summary>
        /// Validates that required parameters are present and conform to basic constraints.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(To))
            {
                throw new ArgumentException("Recipient phone number ('To') cannot be null or whitespace.", nameof(To));
            }

            if (string.IsNullOrWhiteSpace(TemplateName))
            {
                throw new ArgumentException("Template name cannot be null or whitespace.", nameof(TemplateName));
            }

            if (string.IsNullOrWhiteSpace(LanguageCode))
            {
                throw new ArgumentException("Language code cannot be null or whitespace.", nameof(LanguageCode));
            }
        }
    }
}
