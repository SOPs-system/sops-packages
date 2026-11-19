using System;

namespace Sops.Mail.Abstractions
{
    /// <summary>
    /// Represents the outcome of an email transmission attempt.
    /// </summary>
    public sealed class MailSendResult
    {
        /// <summary>
        /// Gets a value indicating whether the email was accepted for delivery.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the server-assigned message identifier or generated tracking ID, if available.
        /// </summary>
        public string? MessageId { get; }

        /// <summary>
        /// Gets the classification of the error if delivery was not successful.
        /// </summary>
        public MailErrorClassification ErrorClassification { get; }

        /// <summary>
        /// Gets a human-readable description of the error if delivery failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the underlying exception that caused delivery to fail, if any.
        /// </summary>
        public Exception? Exception { get; }

        private MailSendResult(
            bool isSuccess,
            string? messageId,
            MailErrorClassification errorClassification,
            string? errorMessage,
            Exception? exception)
        {
            IsSuccess = isSuccess;
            MessageId = messageId;
            ErrorClassification = errorClassification;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful delivery result.
        /// </summary>
        /// <param name="messageId">Optional server or transport message identifier.</param>
        /// <returns>A successful <see cref="MailSendResult"/>.</returns>
        public static MailSendResult Success(string? messageId = null) =>
            new MailSendResult(true, messageId, MailErrorClassification.None, null, null);

        /// <summary>
        /// Creates a failed delivery result.
        /// </summary>
        /// <param name="classification">The error classification.</param>
        /// <param name="errorMessage">The error description.</param>
        /// <param name="exception">Optional underlying exception.</param>
        /// <returns>A failed <see cref="MailSendResult"/>.</returns>
        public static MailSendResult Failure(
            MailErrorClassification classification,
            string errorMessage,
            Exception? exception = null)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Error message cannot be null or whitespace.", nameof(errorMessage));
            }

            return new MailSendResult(false, null, classification, errorMessage, exception);
        }
    }
}
