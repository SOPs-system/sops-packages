using System;
using System.IO;
using System.Text;
using Sops.Mail.Models;
using Xunit;

namespace Sops.Mail.Tests
{
    public class CompositionTests
    {
        [Theory]
        [InlineData("alice@example.com", "Alice Smith")]
        [InlineData("admin@sops.internal", null)]
        [InlineData("support+tickets@company.co.uk", "Support Desk")]
        public void MailRecipient_ValidAddress_InitializesCorrectly(string address, string? displayName)
        {
            var recipient = new MailRecipient(address, displayName);

            Assert.Equal(address, recipient.Address);
            Assert.Equal(displayName, recipient.DisplayName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-an-email")]
        [InlineData("missing-at.com")]
        [InlineData("@missing-user.com")]
        public void MailRecipient_InvalidAddress_ThrowsArgumentException(string? invalidAddress)
        {
            Assert.Throws<ArgumentException>(() => new MailRecipient(invalidAddress!));
        }

        [Theory]
        [InlineData("user@example.com\r\nBcc: evil@attacker.com")]
        [InlineData("user@example.com\nSubject: Injected")]
        [InlineData("user@example.com\r")]
        public void MailRecipient_AddressWithCrlf_ThrowsArgumentException(string injectedAddress)
        {
            var ex = Assert.Throws<ArgumentException>(() => new MailRecipient(injectedAddress));
            Assert.Contains("carriage return or newline", ex.Message);
        }

        [Theory]
        [InlineData("User\r\nName")]
        [InlineData("Admin\nInjected")]
        public void MailRecipient_DisplayNameWithCrlf_ThrowsArgumentException(string injectedName)
        {
            var ex = Assert.Throws<ArgumentException>(() => new MailRecipient("user@example.com", injectedName));
            Assert.Contains("carriage return or newline", ex.Message);
        }

        [Fact]
        public void MailRecipient_ImplicitConversionFromString_Works()
        {
            MailRecipient recipient = "john.doe@example.com";
            Assert.Equal("john.doe@example.com", recipient.Address);
            Assert.Null(recipient.DisplayName);
        }

        [Fact]
        public void MailRecipient_Equality_IsCaseInsensitiveForAddress()
        {
            var r1 = new MailRecipient("alice@example.com", "Alice");
            var r2 = new MailRecipient("ALICE@EXAMPLE.COM", "Alice");
            var r3 = new MailRecipient("bob@example.com", "Alice");

            Assert.Equal(r1, r2);
            Assert.True(r1 == r2);
            Assert.NotEqual(r1, r3);
            Assert.True(r1 != r3);
            Assert.Equal(r1.GetHashCode(), r2.GetHashCode());
        }

        [Fact]
        public void MailRecipient_ToString_FormatsCorrectly()
        {
            var withName = new MailRecipient("user@example.com", "User Name");
            var withoutName = new MailRecipient("user@example.com");

            Assert.Equal("User Name <user@example.com>", withName.ToString());
            Assert.Equal("user@example.com", withoutName.ToString());
        }

        [Fact]
        public void MailAttachment_FromBytes_InitializesProperly()
        {
            byte[] data = Encoding.UTF8.GetBytes("Test Attachment Content");
            var attachment = MailAttachment.FromBytes(data, "document.txt", "text/plain");

            Assert.Equal("document.txt", attachment.FileName);
            Assert.Equal("text/plain", attachment.ContentType);
            Assert.Equal(data, attachment.Content);
            Assert.False(attachment.IsInline);
            Assert.Null(attachment.ContentId);
        }

        [Fact]
        public void MailAttachment_FromStream_InitializesProperly()
        {
            byte[] data = Encoding.UTF8.GetBytes("<html><body>Banner</body></html>");
            using var stream = new MemoryStream(data);

            var attachment = MailAttachment.FromStream(stream, "banner.html", "text/html", isInline: true, contentId: "banner-1");

            Assert.Equal("banner.html", attachment.FileName);
            Assert.Equal("text/html", attachment.ContentType);
            Assert.Equal(data, attachment.Content);
            Assert.True(attachment.IsInline);
            Assert.Equal("banner-1", attachment.ContentId);
        }

        [Fact]
        public void MailAttachment_FromFile_InitializesProperly()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "File on disk content");
                var attachment = MailAttachment.FromFile(tempFile, "text/plain");

                Assert.Equal(Path.GetFileName(tempFile), attachment.FileName);
                Assert.Equal("text/plain", attachment.ContentType);
                Assert.True(attachment.Content.Length > 0);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Theory]
        [InlineData("file\r\nname.pdf")]
        [InlineData("evil\nfile.exe")]
        public void MailAttachment_FileNameWithCrlf_ThrowsArgumentException(string fileName)
        {
            byte[] data = new byte[] { 1, 2, 3 };
            Assert.Throws<ArgumentException>(() => new MailAttachment(fileName, data));
        }

        [Fact]
        public void MailMessage_FluentComposition_PopulatesAllFields()
        {
            var msg = new MailMessage()
                .AddTo("alice@example.com", "Alice")
                .AddCc("bob@example.com", "Bob")
                .AddBcc("audit@example.com")
                .AddReplyTo("replies@example.com", "Support")
                .AddHeader("X-Custom-Tracking", "12345")
                .AddAttachment(MailAttachment.FromBytes(new byte[] { 0x41 }, "test.bin"));

            msg.Subject = "Test Subject";
            msg.TextBody = "Plain text";
            msg.HtmlBody = "<p>Html text</p>";

            Assert.Single(msg.To);
            Assert.Single(msg.Cc);
            Assert.Single(msg.Bcc);
            Assert.Single(msg.ReplyTo);
            Assert.Single(msg.Attachments);
            Assert.Equal("12345", msg.Headers["X-Custom-Tracking"]);
            Assert.Equal("Test Subject", msg.Subject);
            Assert.Equal("Plain text", msg.TextBody);
            Assert.Equal("<p>Html text</p>", msg.HtmlBody);

            // Should pass validation
            msg.Validate();
        }

        [Fact]
        public void MailMessage_Validate_ThrowsWhenNoRecipients()
        {
            var msg = new MailMessage
            {
                Subject = "Hello",
                TextBody = "World"
            };

            var ex = Assert.Throws<InvalidOperationException>(() => msg.Validate());
            Assert.Contains("at least one recipient", ex.Message);
        }

        [Fact]
        public void MailMessage_Validate_ThrowsWhenSubjectIsNull()
        {
            var msg = new MailMessage();
            msg.AddTo("user@example.com");
            msg.Subject = null!;
            msg.TextBody = "World";

            var ex = Assert.Throws<InvalidOperationException>(() => msg.Validate());
            Assert.Contains("subject cannot be null", ex.Message);
        }

        [Theory]
        [InlineData("Subject\r\nBcc: evil@test.com")]
        [InlineData("Subject\nInjected")]
        public void MailMessage_Validate_ThrowsWhenSubjectContainsCrlf(string injectedSubject)
        {
            var msg = new MailMessage();
            msg.AddTo("user@example.com");
            msg.Subject = injectedSubject;
            msg.TextBody = "Content";

            var ex = Assert.Throws<InvalidOperationException>(() => msg.Validate());
            Assert.Contains("carriage return or newline", ex.Message);
        }

        [Fact]
        public void MailMessage_Validate_ThrowsWhenNoBodyOrAttachment()
        {
            var msg = new MailMessage();
            msg.AddTo("user@example.com");
            msg.Subject = "Empty message";

            var ex = Assert.Throws<InvalidOperationException>(() => msg.Validate());
            Assert.Contains("must contain either a plain-text body, an HTML body, or at least one attachment", ex.Message);
        }

        [Theory]
        [InlineData("Header\r\nName", "value")]
        [InlineData("Header-Name", "value\r\nInjected")]
        public void MailMessage_AddHeader_ThrowsOnCrlf(string name, string value)
        {
            var msg = new MailMessage();
            Assert.Throws<ArgumentException>(() => msg.AddHeader(name, value));
        }
    }
}
