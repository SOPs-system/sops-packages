using MailKit.Net.Smtp;
using Sops.Mail.Abstractions;

namespace Sops.Mail.Providers
{
    /// <summary>
    /// Default implementation of <see cref="ISmtpClientFactory"/> that creates standard MailKit <see cref="SmtpClient"/> instances.
    /// </summary>
    public class DefaultSmtpClientFactory : ISmtpClientFactory
    {
        /// <inheritdoc />
        public ISmtpClient CreateClient() => new SmtpClient();
    }
}
