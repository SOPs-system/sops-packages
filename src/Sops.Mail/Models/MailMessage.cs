using System;
using System.Collections.Generic;

namespace Sops.Mail.Models
{
    /// <summary>
    /// Represents an email message to be sent via an <see cref="Abstractions.IMailSender"/>.
    /// </summary>
    public sealed class MailMessage
    {
        /// <summary>
        /// Gets or sets the sender recipient. If null, the default sender configured in settings will be used.
        /// </summary>
        public MailRecipient? From { get; set; }

        /// <summary>
        /// Gets the list of primary recipients.
        /// </summary>
        public IList<MailRecipient> To { get; } = new List<MailRecipient>();

        /// <summary>
        /// Gets the list of carbon copy (CC) recipients.
        /// </summary>
        public IList<MailRecipient> Cc { get; } = new List<MailRecipient>();

        /// <summary>
        /// Gets the list of blind carbon copy (BCC) recipients.
        /// </summary>
        public IList<MailRecipient> Bcc { get; } = new List<MailRecipient>();

        /// <summary>
        /// Gets the list of Reply-To recipients.
        /// </summary>
        public IList<MailRecipient> ReplyTo { get; } = new List<MailRecipient>();

        /// <summary>
        /// Gets or sets the email subject line.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plain-text body of the message.
        /// </summary>
        public string? TextBody { get; set; }

        /// <summary>
        /// Gets or sets the HTML body of the message.
        /// </summary>
        public string? HtmlBody { get; set; }

        /// <summary>
        /// Gets the collection of attachments and inline assets.
        /// </summary>
        public IList<MailAttachment> Attachments { get; } = new List<MailAttachment>();

        /// <summary>
        /// Gets the collection of custom MIME headers.
        /// </summary>
        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new empty instance of the <see cref="MailMessage"/> class.
        /// </summary>
        public MailMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MailMessage"/> class with primary recipient, subject, and bodies.
        /// </summary>
        /// <param name="to">Primary recipient address.</param>
        /// <param name="subject">Email subject line.</param>
        /// <param name="textBody">Optional plain-text body.</param>
        /// <param name="htmlBody">Optional HTML body.</param>
        public MailMessage(string to, string subject, string? textBody = null, string? htmlBody = null)
        {
            AddTo(to);
            Subject = subject ?? string.Empty;
            TextBody = textBody;
            HtmlBody = htmlBody;
        }

        /// <summary>
        /// Adds a primary recipient.
        /// </summary>
        /// <param name="address">Email address.</param>
        /// <param name="displayName">Optional display name.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddTo(string address, string? displayName = null)
        {
            To.Add(new MailRecipient(address, displayName));
            return this;
        }

        /// <summary>
        /// Adds a primary recipient.
        /// </summary>
        /// <param name="recipient">The recipient instance.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddTo(MailRecipient recipient)
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));
            To.Add(recipient);
            return this;
        }

        /// <summary>
        /// Adds a carbon copy (CC) recipient.
        /// </summary>
        /// <param name="address">Email address.</param>
        /// <param name="displayName">Optional display name.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddCc(string address, string? displayName = null)
        {
            Cc.Add(new MailRecipient(address, displayName));
            return this;
        }

        /// <summary>
        /// Adds a carbon copy (CC) recipient.
        /// </summary>
        /// <param name="recipient">The recipient instance.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddCc(MailRecipient recipient)
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));
            Cc.Add(recipient);
            return this;
        }

        /// <summary>
        /// Adds a blind carbon copy (BCC) recipient.
        /// </summary>
        /// <param name="address">Email address.</param>
        /// <param name="displayName">Optional display name.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddBcc(string address, string? displayName = null)
        {
            Bcc.Add(new MailRecipient(address, displayName));
            return this;
        }

        /// <summary>
        /// Adds a blind carbon copy (BCC) recipient.
        /// </summary>
        /// <param name="recipient">The recipient instance.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddBcc(MailRecipient recipient)
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));
            Bcc.Add(recipient);
            return this;
        }

        /// <summary>
        /// Adds a Reply-To recipient.
        /// </summary>
        /// <param name="address">Email address.</param>
        /// <param name="displayName">Optional display name.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddReplyTo(string address, string? displayName = null)
        {
            ReplyTo.Add(new MailRecipient(address, displayName));
            return this;
        }

        /// <summary>
        /// Adds a Reply-To recipient.
        /// </summary>
        /// <param name="recipient">The recipient instance.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddReplyTo(MailRecipient recipient)
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));
            ReplyTo.Add(recipient);
            return this;
        }

        /// <summary>
        /// Adds an attachment to the message.
        /// </summary>
        /// <param name="attachment">The attachment to add.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddAttachment(MailAttachment attachment)
        {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            Attachments.Add(attachment);
            return this;
        }

        /// <summary>
        /// Adds a custom header to the message.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>This message instance for chaining.</returns>
        public MailMessage AddHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Header name cannot be null or whitespace.", nameof(name));
            }

            if (name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Header name cannot contain carriage return or newline characters.", nameof(name));
            }

            if (value != null && (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0))
            {
                throw new ArgumentException("Header value cannot contain carriage return or newline characters.", nameof(value));
            }

            Headers[name.Trim()] = value ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Validates that the message meets minimal delivery criteria before transmission.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when message validation fails.</exception>
        public void Validate()
        {
            if (To.Count == 0 && Cc.Count == 0 && Bcc.Count == 0)
            {
                throw new InvalidOperationException("Email message must have at least one recipient (To, CC, or BCC).");
            }

            if (Subject == null)
            {
                throw new InvalidOperationException("Email subject cannot be null.");
            }

            if (Subject.IndexOf('\r') >= 0 || Subject.IndexOf('\n') >= 0)
            {
                throw new InvalidOperationException("Email subject cannot contain carriage return or newline characters.");
            }

            if (string.IsNullOrEmpty(TextBody) && string.IsNullOrEmpty(HtmlBody) && Attachments.Count == 0)
            {
                throw new InvalidOperationException("Email message must contain either a plain-text body, an HTML body, or at least one attachment.");
            }
        }
    }
}
