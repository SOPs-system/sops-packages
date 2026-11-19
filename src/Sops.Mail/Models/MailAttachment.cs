using System;
using System.IO;

namespace Sops.Mail.Models
{
    /// <summary>
    /// Represents an email attachment or embedded inline asset.
    /// </summary>
    public sealed class MailAttachment
    {
        private const string DefaultContentType = "application/octet-stream";

        /// <summary>
        /// Gets the name of the attached file.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Gets the MIME content type of the attachment.
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Gets the binary content of the attachment.
        /// </summary>
        public byte[] Content { get; }

        /// <summary>
        /// Gets a value indicating whether this attachment is displayed inline within HTML bodies.
        /// </summary>
        public bool IsInline { get; }

        /// <summary>
        /// Gets the Content-ID header value for referencing in HTML markup via cid: URI schemes.
        /// </summary>
        public string? ContentId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MailAttachment"/> class.
        /// </summary>
        /// <param name="fileName">The attachment file name.</param>
        /// <param name="content">The raw bytes of the file.</param>
        /// <param name="contentType">The MIME content type.</param>
        /// <param name="isInline">Whether the attachment is rendered inline.</param>
        /// <param name="contentId">Optional content identifier for inline references.</param>
        public MailAttachment(
            string fileName,
            byte[] content,
            string? contentType = null,
            bool isInline = false,
            string? contentId = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Attachment file name cannot be null or whitespace.", nameof(fileName));
            }

            if (fileName.IndexOf('\r') >= 0 || fileName.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Attachment file name cannot contain carriage return or newline characters.", nameof(fileName));
            }

            FileName = Path.GetFileName(fileName);
            Content = content ?? throw new ArgumentNullException(nameof(content));
            ContentType = string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType!.Trim();
            IsInline = isInline;
            ContentId = contentId?.Trim();
        }

        /// <summary>
        /// Creates an attachment from a byte array.
        /// </summary>
        /// <param name="content">The raw bytes.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="contentType">Optional MIME type.</param>
        /// <param name="isInline">Whether the attachment is inline.</param>
        /// <param name="contentId">Optional Content-ID.</param>
        /// <returns>A new <see cref="MailAttachment"/> instance.</returns>
        public static MailAttachment FromBytes(
            byte[] content,
            string fileName,
            string? contentType = null,
            bool isInline = false,
            string? contentId = null)
        {
            return new MailAttachment(fileName, content, contentType, isInline, contentId);
        }

        /// <summary>
        /// Creates an attachment by reading the complete contents of a stream.
        /// </summary>
        /// <param name="stream">The source stream to read from.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="contentType">Optional MIME type.</param>
        /// <param name="isInline">Whether the attachment is inline.</param>
        /// <param name="contentId">Optional Content-ID.</param>
        /// <returns>A new <see cref="MailAttachment"/> instance.</returns>
        public static MailAttachment FromStream(
            Stream stream,
            string fileName,
            string? contentType = null,
            bool isInline = false,
            string? contentId = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] bytes;
            if (stream is MemoryStream ms)
            {
                bytes = ms.ToArray();
            }
            else
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }

            return new MailAttachment(fileName, bytes, contentType, isInline, contentId);
        }

        /// <summary>
        /// Creates an attachment by reading a file from disk.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="contentType">Optional MIME type.</param>
        /// <param name="isInline">Whether the attachment is inline.</param>
        /// <param name="contentId">Optional Content-ID.</param>
        /// <returns>A new <see cref="MailAttachment"/> instance.</returns>
        public static MailAttachment FromFile(
            string filePath,
            string? contentType = null,
            bool isInline = false,
            string? contentId = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
            }

            byte[] bytes = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);

            return new MailAttachment(fileName, bytes, contentType, isInline, contentId);
        }
    }
}
