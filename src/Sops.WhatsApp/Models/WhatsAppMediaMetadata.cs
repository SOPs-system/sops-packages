namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents metadata retrieved for an uploaded WhatsApp media object.
    /// </summary>
    public class WhatsAppMediaMetadata
    {
        /// <summary>
        /// Gets or sets the Meta Graph API media ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the temporary direct download URL provided by Meta.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the media file.
        /// </summary>
        public string? MimeType { get; set; }

        /// <summary>
        /// Gets or sets the SHA256 cryptographic hash of the media file.
        /// </summary>
        public string? Sha256 { get; set; }

        /// <summary>
        /// Gets or sets the size of the media file in bytes.
        /// </summary>
        public long FileSize { get; set; }
    }
}
