using System;
using System.Reflection;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Sops.Mail.Configuration;
using Sops.Mail.Models;
using Xunit;

namespace Sops.Mail.Tests
{
    public class SecurityTests
    {
        [Fact]
        public void Password_HasSensitiveAttribute()
        {
            var prop = typeof(SmtpSettings).GetProperty(nameof(SmtpSettings.Password));
            Assert.NotNull(prop);
            var sensitiveAttr = prop.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(sensitiveAttr);
        }

        [Fact]
        public void Password_IsMaskedBySecretMasker()
        {
            var prop = typeof(SmtpSettings).GetProperty(nameof(SmtpSettings.Password));
            Assert.NotNull(prop);
            Assert.True(SecretMasker.IsSensitive(prop!));

            string rawSecret = "SuperSecretPassword123!";
            string masked = SecretMasker.Mask(rawSecret);
            Assert.Equal(SecretMasker.RedactedPlaceholder, masked);

            string diagnosticMessage = $"Failed connecting with password {rawSecret} to server.";
            string sanitized = SecretMasker.RedactValueFromMessage(diagnosticMessage, rawSecret);
            Assert.DoesNotContain(rawSecret, sanitized);
            Assert.Contains(SecretMasker.RedactedPlaceholder, sanitized);
        }

        [Theory]
        [InlineData("admin@example.com\r\nBcc: spy@attacker.com")]
        [InlineData("admin@example.com\nSubject: Injected")]
        public void Recipient_CrlfInjection_IsRejected(string injected)
        {
            var ex = Assert.Throws<ArgumentException>(() => new MailRecipient(injected));
            Assert.Contains("carriage return or newline", ex.Message);
        }

        [Theory]
        [InlineData("Alert\r\nTo: victim@example.com")]
        [InlineData("Alert\nInjected")]
        public void Subject_CrlfInjection_IsRejected(string injectedSubject)
        {
            var msg = new MailMessage();
            msg.AddTo("user@example.com");
            msg.Subject = injectedSubject;
            msg.TextBody = "Safe body";

            var ex = Assert.Throws<InvalidOperationException>(() => msg.Validate());
            Assert.Contains("carriage return or newline", ex.Message);
        }

        [Theory]
        [InlineData("X-Header\r\nInjected", "NormalValue")]
        [InlineData("X-Header", "Value\r\nInjected: evil")]
        public void Header_CrlfInjection_IsRejected(string name, string value)
        {
            var msg = new MailMessage();
            var ex = Assert.Throws<ArgumentException>(() => msg.AddHeader(name, value));
            Assert.Contains("carriage return or newline", ex.Message);
        }

        [Theory]
        [InlineData("file\r\nname.pdf")]
        [InlineData("file\nname.pdf")]
        public void Attachment_CrlfInjection_IsRejected(string fileName)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new MailAttachment(fileName, new byte[] { 1, 2, 3 }));
            Assert.Contains("carriage return or newline", ex.Message);
        }
    }
}
