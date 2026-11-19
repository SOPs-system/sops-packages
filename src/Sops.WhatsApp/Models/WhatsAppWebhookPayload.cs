using System;
using System.Collections.Generic;
using Sops.WhatsApp.Abstractions;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents an event parsed from a Meta WhatsApp Business Platform webhook notification.
    /// </summary>
    public class WhatsAppWebhookEvent
    {
        /// <summary>
        /// Gets or sets the WhatsApp Business Account ID (WABA ID) associated with the event.
        /// </summary>
        public string? BusinessAccountId { get; set; }

        /// <summary>
        /// Gets or sets the Meta phone number ID that received the event.
        /// </summary>
        public string? PhoneNumberId { get; set; }

        /// <summary>
        /// Gets or sets the display phone number associated with the receiving phone number ID.
        /// </summary>
        public string? DisplayPhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets incoming user messages parsed from this webhook notification.
        /// </summary>
        public List<WhatsAppIncomingMessage> IncomingMessages { get; set; } = new List<WhatsAppIncomingMessage>();

        /// <summary>
        /// Gets or sets message delivery status updates parsed from this webhook notification.
        /// </summary>
        public List<WhatsAppStatusUpdate> StatusUpdates { get; set; } = new List<WhatsAppStatusUpdate>();
    }

    /// <summary>
    /// Represents an inbound message sent by a WhatsApp user to the business.
    /// </summary>
    public class WhatsAppIncomingMessage
    {
        /// <summary>
        /// Gets or sets the unique WhatsApp message identifier (e.g. "wamid.HBgL...").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender's phone number.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the message was sent.
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the message type (e.g. "text", "image", "document", "audio", "video", "button").
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>
        /// Gets or sets the text body if this is a text message.
        /// </summary>
        public string? TextBody { get; set; }

        /// <summary>
        /// Gets or sets the media information if this is a media message.
        /// </summary>
        public WhatsAppMediaReference? Media { get; set; }
    }

    /// <summary>
    /// Represents an outbound message delivery status transition (sent, delivered, read, failed).
    /// </summary>
    public class WhatsAppStatusUpdate
    {
        /// <summary>
        /// Gets or sets the message ID being updated.
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipient phone number.
        /// </summary>
        public string RecipientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new delivery status.
        /// </summary>
        public WhatsAppDeliveryStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the status changed.
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// Gets or sets any delivery failure errors reported by Meta.
        /// </summary>
        public List<WhatsAppWebhookError> Errors { get; set; } = new List<WhatsAppWebhookError>();
    }

    /// <summary>
    /// Details of a failure or error reported in a webhook status update.
    /// </summary>
    public class WhatsAppWebhookError
    {
        /// <summary>
        /// Gets or sets the Meta error code (e.g. 131026 for undeliverable).
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Gets or sets the short error title or summary.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detailed error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional error details if provided by Meta.
        /// </summary>
        public string? Details { get; set; }
    }
}
