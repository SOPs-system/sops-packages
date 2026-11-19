using MailKit.Net.Smtp;

namespace Sops.Mail.Abstractions
{
    /// <summary>
    /// Factory for creating MailKit <see cref="ISmtpClient"/> instances, enabling unit testing and custom client configuration.
    /// </summary>
    public interface ISmtpClientFactory
    {
        /// <summary>
        /// Creates a new instance of an SMTP client.
        /// </summary>
        /// <returns>An <see cref="ISmtpClient"/> instance.</returns>
        ISmtpClient CreateClient();
    }
}
