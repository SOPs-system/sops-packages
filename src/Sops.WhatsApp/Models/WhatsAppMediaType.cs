namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Supported media types in WhatsApp Business Platform Cloud API.
    /// </summary>
    public enum WhatsAppMediaType
    {
        /// <summary>
        /// Image media (JPEG, PNG).
        /// </summary>
        Image = 1,

        /// <summary>
        /// Document media (PDF, DOCX, etc.).
        /// </summary>
        Document = 2,

        /// <summary>
        /// Audio media (AAC, MP4, AMR, MP3, OGG).
        /// </summary>
        Audio = 3,

        /// <summary>
        /// Video media (MP4, 3GP).
        /// </summary>
        Video = 4,

        /// <summary>
        /// Sticker media (WebP).
        /// </summary>
        Sticker = 5
    }
}
