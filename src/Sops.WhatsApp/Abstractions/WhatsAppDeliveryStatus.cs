namespace Sops.WhatsApp.Abstractions
{
    /// <summary>
    /// Represents the delivery status of a WhatsApp message.
    /// </summary>
    public enum WhatsAppDeliveryStatus
    {
        /// <summary>
        /// The delivery status is unknown or not yet determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The message was accepted by Meta Cloud API and queued or dispatched.
        /// </summary>
        Sent = 1,

        /// <summary>
        /// The message has been successfully delivered to the recipient's device.
        /// </summary>
        Delivered = 2,

        /// <summary>
        /// The message has been viewed or read by the recipient.
        /// </summary>
        Read = 3,

        /// <summary>
        /// The message delivery has failed.
        /// </summary>
        Failed = 4
    }
}
