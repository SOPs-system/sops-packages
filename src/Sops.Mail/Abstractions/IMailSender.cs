using System.Threading;
using System.Threading.Tasks;
using Sops.Mail.Models;

namespace Sops.Mail.Abstractions
{
    /// <summary>
    /// Provides asynchronous email transmission capabilities.
    /// </summary>
    public interface IMailSender
    {
        /// <summary>
        /// Asynchronously transmits the specified email message.
        /// </summary>
        /// <param name="message">The email message to send.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation, returning a <see cref="MailSendResult"/>.</returns>
        Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken = default);
    }
}
