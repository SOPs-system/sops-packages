namespace Sops.Payments.Abstractions
{
    /// <summary>
    /// Represents the standardized lifecycle status of a payment transaction.
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>
        /// The payment has been initiated and is awaiting customer action, redirection, or bank authorization.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// The payment was successfully authorized and captured.
        /// </summary>
        Successful = 1,

        /// <summary>
        /// The payment failed or was declined by the payment provider or issuing bank.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// The payment was cancelled by the customer or voided before capture.
        /// </summary>
        Cancelled = 3,

        /// <summary>
        /// The payment was previously successful but has since been refunded (partially or fully).
        /// </summary>
        Refunded = 4,

        /// <summary>
        /// The state of the payment cannot be determined with certainty and requires reconciliation.
        /// </summary>
        Unknown = 5
    }
}
