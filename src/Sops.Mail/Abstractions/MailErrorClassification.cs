namespace Sops.Mail.Abstractions
{
    /// <summary>
    /// Classifies email delivery errors to assist retry policies and alerting systems.
    /// </summary>
    public enum MailErrorClassification
    {
        /// <summary>
        /// No error occurred. The message was accepted by the transport.
        /// </summary>
        None = 0,

        /// <summary>
        /// A transient failure occurred (e.g. temporary network glitch, server busy, 4xx SMTP code, rate limit).
        /// Safe to retry according to policy.
        /// </summary>
        Transient = 1,

        /// <summary>
        /// A permanent failure occurred (e.g. invalid recipient address, authentication failure, 5xx SMTP code, invalid configuration).
        /// Should not be retried without modifying the message or credentials.
        /// </summary>
        Permanent = 2,

        /// <summary>
        /// An ambiguous failure occurred (e.g. socket timeout during or immediately after the DATA command).
        /// The transport may or may not have accepted the message; retrying automatically could result in duplicate deliveries.
        /// </summary>
        Ambiguous = 3
    }
}
