namespace Sops.Payments.Abstractions
{
    /// <summary>
    /// Classifies payment processing errors to determine appropriate retry and alerting behavior.
    /// </summary>
    public enum PaymentErrorClassification
    {
        /// <summary>
        /// No error occurred. The operation succeeded.
        /// </summary>
        None = 0,

        /// <summary>
        /// A transient failure occurred (e.g. gateway network timeout, rate limit, 5xx server error).
        /// Safe to retry only if an idempotency key is maintained.
        /// </summary>
        Transient = 1,

        /// <summary>
        /// A permanent failure occurred (e.g. invalid credentials, card declined, invalid currency, malformed payload).
        /// Do not retry without modifying the request parameters.
        /// </summary>
        Permanent = 2,

        /// <summary>
        /// The outcome is ambiguous (e.g. network dropped while awaiting authorization response).
        /// Automatic retries MUST NOT be attempted to prevent duplicate charges; reconciliation is required.
        /// </summary>
        Ambiguous = 3
    }
}
