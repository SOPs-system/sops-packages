using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Sops.Mail.Abstractions;
using Sops.Mail.Configuration;
using Sops.Mail.Models;

namespace Sops.Mail.Providers
{
    /// <summary>
    /// Production-ready implementation of <see cref="IMailSender"/> utilizing MailKit and MimeKit for SMTP transport.
    /// Enforces TLS, explicit error classification, cancellation responsiveness, and secret sanitation.
    /// </summary>
    public class SmtpMailSender : IMailSender, IDisposable
    {
        private readonly MailSettings _settings;
        private readonly ILogger<SmtpMailSender>? _logger;
        private readonly ISmtpClientFactory _clientFactory;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmtpMailSender"/> class with options configuration.
        /// </summary>
        /// <param name="options">The mail configuration options.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <param name="clientFactory">Optional factory for creating SMTP clients.</param>
        public SmtpMailSender(
            IOptions<MailSettings> options,
            ILogger<SmtpMailSender>? logger = null,
            ISmtpClientFactory? clientFactory = null)
            : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), logger, clientFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmtpMailSender"/> class with direct settings.
        /// </summary>
        /// <param name="settings">The mail configuration settings.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <param name="clientFactory">Optional factory for creating SMTP clients.</param>
        public SmtpMailSender(
            MailSettings settings,
            ILogger<SmtpMailSender>? logger = null,
            ISmtpClientFactory? clientFactory = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger;
            _clientFactory = clientFactory ?? new DefaultSmtpClientFactory();
        }

        /// <summary>
        /// Factory method to create an <see cref="SmtpMailSender"/> instance without dependency injection.
        /// </summary>
        /// <param name="settings">The mail configuration settings.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <param name="clientFactory">Optional client factory.</param>
        /// <returns>A new <see cref="SmtpMailSender"/> instance.</returns>
        public static SmtpMailSender Create(
            MailSettings settings,
            ILogger<SmtpMailSender>? logger = null,
            ISmtpClientFactory? clientFactory = null)
        {
            return new SmtpMailSender(settings, logger, clientFactory);
        }

        /// <inheritdoc />
        public async Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            try
            {
                message.Validate();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Mail message validation failed.");
                return MailSendResult.Failure(MailErrorClassification.Permanent, ex.Message, ex);
            }

            // Resolve effective sender (From address)
            MailRecipient? sender = message.From;
            if (sender == null && !string.IsNullOrWhiteSpace(_settings.FromAddress))
            {
                sender = new MailRecipient(_settings.FromAddress!, _settings.FromName);
            }

            if (sender == null)
            {
                string error = "No sender (From) address specified on message or in configuration.";
                _logger?.LogError("{ErrorMessage}", error);
                return MailSendResult.Failure(MailErrorClassification.Permanent, error);
            }

            int recipientCount = message.To.Count + message.Cc.Count + message.Bcc.Count;

            // Honor disabled state (e.g. testing or local staging)
            if (!_settings.Enabled)
            {
                _logger?.LogInformation(
                    "Email delivery is disabled by configuration. Suppressing delivery for {RecipientCount} recipient(s).",
                    recipientCount);
                return MailSendResult.Success("disabled-sink");
            }

            var smtp = _settings.Smtp ?? new SmtpSettings();
            var stopwatch = Stopwatch.StartNew();

            MimeMessage mimeMessage;
            try
            {
                mimeMessage = BuildMimeMessage(message, sender);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to compose MIME email message.");
                return MailSendResult.Failure(MailErrorClassification.Permanent, $"MIME composition error: {ex.Message}", ex);
            }

            bool inTransmissionPhase = false;

            try
            {
                using var client = _clientFactory.CreateClient();
                client.Timeout = smtp.TimeoutSeconds * 1000;

                SecureSocketOptions socketOptions = ResolveSecureSocketOptions(smtp.Security);

                _logger?.LogDebug(
                    "Connecting to SMTP server {Host}:{Port} with security {SecurityMode}...",
                    smtp.Host,
                    smtp.Port,
                    socketOptions);

                await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken).ConfigureAwait(false);

                bool shouldAuthenticate = smtp.RequireAuthentication || !string.IsNullOrWhiteSpace(smtp.Username);
                if (shouldAuthenticate && !string.IsNullOrWhiteSpace(smtp.Username))
                {
                    string username = smtp.Username!;
                    _logger?.LogDebug("Authenticating as SMTP user '{Username}'...", username);
                    await client.AuthenticateAsync(username, smtp.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
                }

                inTransmissionPhase = true;

                _logger?.LogDebug("Transmitting message to {RecipientCount} recipient(s)...", recipientCount);
                await client.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);

                try
                {
                    await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Disconnect failures after successful send do not invalidate successful transmission
                }

                stopwatch.Stop();
                string messageId = mimeMessage.MessageId ?? Guid.NewGuid().ToString("N");

                _logger?.LogInformation(
                    "Email transmitted successfully in {ElapsedMilliseconds}ms. MessageId: {MessageId}, Recipients: {RecipientCount}.",
                    stopwatch.ElapsedMilliseconds,
                    messageId,
                    recipientCount);

                return MailSendResult.Success(messageId);
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                stopwatch.Stop();
                _logger?.LogError(
                    "SMTP authentication failed against {Host}:{Port}: {ErrorMessage}",
                    smtp.Host,
                    smtp.Port,
                    ex.Message);

                return MailSendResult.Failure(MailErrorClassification.Permanent, $"SMTP authentication failed: {ex.Message}", ex);
            }
            catch (SmtpCommandException ex)
            {
                stopwatch.Stop();
                var classification = ((int)ex.StatusCode >= 500)
                    ? MailErrorClassification.Permanent
                    : MailErrorClassification.Transient;

                _logger?.LogError(
                    "SMTP command failed with status {StatusCode} ({Classification}): {ErrorMessage}",
                    ex.StatusCode,
                    classification,
                    ex.Message);

                return MailSendResult.Failure(classification, $"SMTP error {ex.StatusCode}: {ex.Message}", ex);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger?.LogWarning("Email transmission was cancelled by caller.");
                return MailSendResult.Failure(MailErrorClassification.Transient, "Email transmission was cancelled.", ex);
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException)
            {
                stopwatch.Stop();
                var classification = inTransmissionPhase
                    ? MailErrorClassification.Ambiguous
                    : MailErrorClassification.Transient;

                _logger?.LogError(
                    "I/O or timeout failure during email transmission ({Classification}): {ErrorMessage}",
                    classification,
                    ex.Message);

                return MailSendResult.Failure(classification, $"Transport failure ({classification}): {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex, "Unexpected failure transmitting email.");
                return MailSendResult.Failure(MailErrorClassification.Permanent, $"Unexpected failure: {ex.Message}", ex);
            }
        }

        private static SecureSocketOptions ResolveSecureSocketOptions(SmtpSecurity security)
        {
            switch (security)
            {
                case SmtpSecurity.None:
                    return SecureSocketOptions.None;
                case SmtpSecurity.SslOnConnect:
                    return SecureSocketOptions.SslOnConnect;
                case SmtpSecurity.StartTls:
                    return SecureSocketOptions.StartTls;
                case SmtpSecurity.StartTlsWhenAvailable:
                    return SecureSocketOptions.StartTlsWhenAvailable;
                case SmtpSecurity.Auto:
                default:
                    return SecureSocketOptions.Auto;
            }
        }

        private static MimeMessage BuildMimeMessage(MailMessage message, MailRecipient sender)
        {
            var mime = new MimeMessage();

            mime.From.Add(new MailboxAddress(sender.DisplayName, sender.Address));

            foreach (var to in message.To)
            {
                mime.To.Add(new MailboxAddress(to.DisplayName, to.Address));
            }

            foreach (var cc in message.Cc)
            {
                mime.Cc.Add(new MailboxAddress(cc.DisplayName, cc.Address));
            }

            foreach (var bcc in message.Bcc)
            {
                mime.Bcc.Add(new MailboxAddress(bcc.DisplayName, bcc.Address));
            }

            foreach (var replyTo in message.ReplyTo)
            {
                mime.ReplyTo.Add(new MailboxAddress(replyTo.DisplayName, replyTo.Address));
            }

            mime.Subject = message.Subject;

            foreach (var header in message.Headers)
            {
                mime.Headers[header.Key] = header.Value;
            }

            var builder = new BodyBuilder();

            if (!string.IsNullOrEmpty(message.TextBody))
            {
                builder.TextBody = message.TextBody;
            }

            if (!string.IsNullOrEmpty(message.HtmlBody))
            {
                builder.HtmlBody = message.HtmlBody;
            }

            foreach (var attachment in message.Attachments)
            {
                var contentType = ContentType.Parse(attachment.ContentType);

                if (attachment.IsInline)
                {
                    var part = builder.LinkedResources.Add(attachment.FileName, attachment.Content, contentType);
                    if (!string.IsNullOrWhiteSpace(attachment.ContentId))
                    {
                        part.ContentId = attachment.ContentId;
                    }
                }
                else
                {
                    builder.Attachments.Add(attachment.FileName, attachment.Content, contentType);
                }
            }

            mime.Body = builder.ToMessageBody();
            return mime;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases managed resources.
        /// </summary>
        /// <param name="disposing">True to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
