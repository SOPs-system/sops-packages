namespace Sops.WhatsApp.Abstractions
{
    /// <summary>
    /// Classifies an error encountered during WhatsApp operations to guide retry and recovery decisions.
    /// </summary>
    public enum WhatsAppErrorClassification
    {
        /// <summary>
        /// No error occurred.
        /// </summary>
        None = 0,

        /// <summary>
        /// A transient error occurred (e.g. rate limit, temporary upstream HTTP 500/503), which may succeed upon retry.
        /// </summary>
        Transient = 1,

        /// <summary>
        /// A permanent error occurred (e.g. invalid phone number, invalid template name, authentication failure), which will not succeed without changes.
        /// </summary>
        Permanent = 2,

        /// <summary>
        /// An ambiguous error occurred (e.g. network timeout or connection reset after sending request payload).
        /// The message may or may not have been delivered; callers must not blindly retry without status checks or deduplication.
        /// </summary>
        Ambiguous = 3
    }
}
