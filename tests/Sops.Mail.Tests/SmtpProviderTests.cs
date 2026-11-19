using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sops.Mail.Abstractions;
using Sops.Mail.Configuration;
using Sops.Mail.Models;
using Sops.Mail.Providers;
using Xunit;

namespace Sops.Mail.Tests
{
    public class SmtpProviderTests
    {
        private readonly ISmtpClient _mockClient;
        private readonly ISmtpClientFactory _mockFactory;

        public SmtpProviderTests()
        {
            _mockClient = Substitute.For<ISmtpClient>();
            _mockFactory = Substitute.For<ISmtpClientFactory>();
            _mockFactory.CreateClient().Returns(_mockClient);
        }

        [Fact]
        public async Task SendAsync_SuccessfulDelivery_ConnectsAuthenticatesSendsAndDisconnects()
        {
            var settings = new MailSettings
            {
                FromAddress = "noreply@sops.internal",
                FromName = "SOPs Service",
                Smtp = new SmtpSettings
                {
                    Host = "smtp.example.com",
                    Port = 587,
                    Security = SmtpSecurity.StartTls,
                    Username = "smtp-user",
                    Password = "smtp-password",
                    TimeoutSeconds = 15
                }
            };

            MimeMessage? sentMime = null;
            _mockClient.SendAsync(Arg.Do<MimeMessage>(m => sentMime = m), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("250 OK"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);

            var message = new MailMessage
            {
                Subject = "Quarterly Report",
                TextBody = "Report details attached.",
                HtmlBody = "<p>Report details attached.</p>"
            };
            message.AddTo("manager@example.com", "Manager");
            message.AddCc("audit@example.com");

            var result = await sender.SendAsync(message);

            Assert.True(result.IsSuccess);
            Assert.Equal(MailErrorClassification.None, result.ErrorClassification);
            Assert.NotNull(result.MessageId);
            Assert.Null(result.ErrorMessage);

            await _mockClient.Received(1).ConnectAsync(
                "smtp.example.com",
                587,
                SecureSocketOptions.StartTls,
                Arg.Any<CancellationToken>());

            await _mockClient.Received(1).AuthenticateAsync(
                "smtp-user",
                "smtp-password",
                Arg.Any<CancellationToken>());

            await _mockClient.Received(1).SendAsync(
                Arg.Any<MimeMessage>(),
                Arg.Any<CancellationToken>());

            await _mockClient.Received(1).DisconnectAsync(
                true,
                Arg.Any<CancellationToken>());

            Assert.NotNull(sentMime);
            Assert.Equal("Quarterly Report", sentMime!.Subject);
            Assert.Equal("SOPs Service", sentMime.From[0].Name);
            Assert.Equal("noreply@sops.internal", ((MailboxAddress)sentMime.From[0]).Address);
            Assert.Equal("manager@example.com", ((MailboxAddress)sentMime.To[0]).Address);
            Assert.Equal("audit@example.com", ((MailboxAddress)sentMime.Cc[0]).Address);
        }

        [Fact]
        public async Task SendAsync_DisabledViaConfiguration_SuppressesDelivery()
        {
            var settings = new MailSettings
            {
                Enabled = false,
                FromAddress = "noreply@sops.internal"
            };

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);

            var message = new MailMessage("recipient@example.com", "Subject", "Body");

            var result = await sender.SendAsync(message);

            Assert.True(result.IsSuccess);
            Assert.Equal("disabled-sink", result.MessageId);
            _mockFactory.DidNotReceive().CreateClient();
        }

        [Fact]
        public async Task SendAsync_FallsBackToConfiguredFromAddress_WhenMessageFromIsNull()
        {
            var settings = new MailSettings
            {
                FromAddress = "default-sender@example.com",
                FromName = "Default Name"
            };

            MimeMessage? sentMime = null;
            _mockClient.SendAsync(Arg.Do<MimeMessage>(m => sentMime = m), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("250 OK"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.True(result.IsSuccess);
            Assert.NotNull(sentMime);
            Assert.Equal("default-sender@example.com", ((MailboxAddress)sentMime!.From[0]).Address);
            Assert.Equal("Default Name", sentMime.From[0].Name);
        }

        [Fact]
        public async Task SendAsync_ExplicitMessageFrom_OverridesConfiguredFromAddress()
        {
            var settings = new MailSettings
            {
                FromAddress = "default-sender@example.com",
                FromName = "Default Name"
            };

            MimeMessage? sentMime = null;
            _mockClient.SendAsync(Arg.Do<MimeMessage>(m => sentMime = m), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("250 OK"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body")
            {
                From = new MailRecipient("custom-sender@example.com", "Custom Sender")
            };

            var result = await sender.SendAsync(message);

            Assert.True(result.IsSuccess);
            Assert.NotNull(sentMime);
            Assert.Equal("custom-sender@example.com", ((MailboxAddress)sentMime!.From[0]).Address);
            Assert.Equal("Custom Sender", sentMime.From[0].Name);
        }

        [Fact]
        public async Task SendAsync_NoSenderAddressAnywhere_ReturnsPermanentFailure()
        {
            var settings = new MailSettings
            {
                FromAddress = null
            };

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body")
            {
                From = null
            };

            var result = await sender.SendAsync(message);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Permanent, result.ErrorClassification);
            Assert.Contains("No sender (From) address", result.ErrorMessage);
            _mockFactory.DidNotReceive().CreateClient();
        }

        [Fact]
        public async Task SendAsync_AuthenticationFailure_ClassifiedAsPermanent()
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com",
                Smtp = new SmtpSettings
                {
                    Host = "smtp.example.com",
                    Username = "bad-user",
                    Password = "bad-password"
                }
            };

            _mockClient.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new MailKit.Security.AuthenticationException("535 5.7.8 Authentication credentials invalid"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Permanent, result.ErrorClassification);
            Assert.Contains("Authentication credentials invalid", result.ErrorMessage);
        }

        [Fact]
        public async Task SendAsync_Smtp5xxError_ClassifiedAsPermanent()
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com"
            };

            _mockClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 5.1.1 User unknown"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("unknown@example.com", "Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Permanent, result.ErrorClassification);
            Assert.Contains("550", result.ErrorMessage);
        }

        [Fact]
        public async Task SendAsync_Smtp4xxError_ClassifiedAsTransient()
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com"
            };

            _mockClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable, "421 Service temporarily unavailable"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Transient, result.ErrorClassification);
            Assert.Contains("421", result.ErrorMessage);
        }

        [Fact]
        public async Task SendAsync_TimeoutConnecting_ClassifiedAsTransient()
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com"
            };

            _mockClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new TimeoutException("Connection timed out after 30 seconds"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Transient, result.ErrorClassification);
            Assert.Contains("timed out", result.ErrorMessage);
        }

        [Fact]
        public async Task SendAsync_IoExceptionDuringDataTransmission_ClassifiedAsAmbiguous()
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com"
            };

            _mockClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new IOException("Socket forcibly closed by remote host during DATA"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Ambiguous, result.ErrorClassification);
            Assert.Contains("Ambiguous", result.ErrorMessage);
        }

        [Fact]
        public async Task SendAsync_CallerCancellation_ClassifiedAsTransient()
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com"
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException(cts.Token));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Test", "Body");

            var result = await sender.SendAsync(message, cts.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal(MailErrorClassification.Transient, result.ErrorClassification);
            Assert.Contains("cancelled", result.ErrorMessage);
        }

        [Theory]
        [InlineData(SmtpSecurity.None, SecureSocketOptions.None)]
        [InlineData(SmtpSecurity.SslOnConnect, SecureSocketOptions.SslOnConnect)]
        [InlineData(SmtpSecurity.StartTls, SecureSocketOptions.StartTls)]
        [InlineData(SmtpSecurity.StartTlsWhenAvailable, SecureSocketOptions.StartTlsWhenAvailable)]
        [InlineData(SmtpSecurity.Auto, SecureSocketOptions.Auto)]
        public async Task SendAsync_SecurityModes_MappedToCorrectMailKitOptions(SmtpSecurity security, SecureSocketOptions expected)
        {
            var settings = new MailSettings
            {
                FromAddress = "sender@example.com",
                Smtp = new SmtpSettings
                {
                    Host = "smtp.custom.org",
                    Port = 2525,
                    Security = security
                }
            };

            _mockClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("250 OK"));

            var sender = new SmtpMailSender(settings, clientFactory: _mockFactory);
            var message = new MailMessage("to@example.com", "Security Test", "Body");

            var result = await sender.SendAsync(message);

            Assert.True(result.IsSuccess);
            await _mockClient.Received(1).ConnectAsync(
                "smtp.custom.org",
                2525,
                expected,
                Arg.Any<CancellationToken>());
        }
    }
}
