using System;
using System.IO;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents a request to upload a media file directly to Meta's WhatsApp servers.
    /// </summary>
    public class WhatsAppMediaUploadRequest
    {
        /// <summary>
        /// Gets or sets the name of the file (e.g. "invoice.pdf", "photo.jpg").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MIME type of the file (e.g. "application/pdf", "image/png").
        /// </summary>
        public string MimeType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content stream of the file.
        /// </summary>
        public Stream? Content { get; set; }

        /// <summary>
        /// Gets or sets the raw bytes of the file, if stream is not provided.
        /// </summary>
        public byte[]? ContentBytes { get; set; }

        /// <summary>
        /// Validates that the request has valid metadata and file content.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException("FileName cannot be null or whitespace.", nameof(FileName));
            }

            if (string.IsNullOrWhiteSpace(MimeType))
            {
                throw new ArgumentException("MimeType cannot be null or whitespace.", nameof(MimeType));
            }

            if (Content == null && (ContentBytes == null || ContentBytes.Length == 0))
            {
                throw new ArgumentException("Either Content stream or non-empty ContentBytes must be provided.", nameof(Content));
            }
        }
    }
}
