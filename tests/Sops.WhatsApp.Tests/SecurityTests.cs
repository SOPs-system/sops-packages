using System.Security.Cryptography;
using System.Text;
using Sops.WhatsApp.Security;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class SecurityTests
    {
        [Theory]
        [InlineData("+201001234567", "201001234567")]
        [InlineData("+1 (555) 025-4583", "15550254583")]
        [InlineData(" 20-100-123-4567 ", "201001234567")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void NormalizePhoneNumber_StripsNonDigits(string? input, string expected)
        {
            string actual = WhatsAppSecurityHelper.NormalizePhoneNumber(input!);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("+201001234567", "+20*****4567")]
        [InlineData("201001234567", "201*****4567")]
        [InlineData("1234567", "12***67")]
        [InlineData("1234", "****")]
        [InlineData("", "[EMPTY]")]
        [InlineData(null, "[EMPTY]")]
        public void MaskPhoneNumber_HidesMiddleDigits(string? input, string expected)
        {
            string actual = WhatsAppSecurityHelper.MaskPhoneNumber(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void MaskToken_HidesSensitiveContent()
        {
            Assert.Equal("[REDACTED]", WhatsAppSecurityHelper.MaskToken(null));
            Assert.Equal("[REDACTED]", WhatsAppSecurityHelper.MaskToken(""));
            Assert.Equal("******", WhatsAppSecurityHelper.MaskToken("12345"));
            Assert.Equal("EAA...xyz", WhatsAppSecurityHelper.MaskToken("EAAB123456789xyz"));
        }

        [Fact]
        public void ComputeHmacSha256_MatchesStandardImplementation()
        {
            string payload = "{\"object\":\"whatsapp_business_account\"}";
            string secret = "test_meta_app_secret_123";

            string actual = WhatsAppSecurityHelper.ComputeHmacSha256(payload, secret);

            // Compute independently
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder();
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                string expected = sb.ToString();

                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void VerifyWebhookSignature_ValidSignature_ReturnsTrue()
        {
            string payload = "{\"entry\":[{\"id\":\"12345\"}]}";
            string secret = "my_super_secret_app_key";
            string hash = WhatsAppSecurityHelper.ComputeHmacSha256(payload, secret);
            string header = $"sha256={hash}";

            bool isValid = WhatsAppSecurityHelper.VerifyWebhookSignature(header, payload, secret);
            Assert.True(isValid);
        }

        [Fact]
        public void VerifyWebhookSignature_TamperedPayload_ReturnsFalse()
        {
            string payload = "{\"entry\":[{\"id\":\"12345\"}]}";
            string tampered = "{\"entry\":[{\"id\":\"99999\"}]}";
            string secret = "my_super_secret_app_key";
            string hash = WhatsAppSecurityHelper.ComputeHmacSha256(payload, secret);
            string header = $"sha256={hash}";

            bool isValid = WhatsAppSecurityHelper.VerifyWebhookSignature(header, tampered, secret);
            Assert.False(isValid);
        }

        [Fact]
        public void VerifyWebhookSignature_WrongSecret_ReturnsFalse()
        {
            string payload = "{\"entry\":[{\"id\":\"12345\"}]}";
            string hash = WhatsAppSecurityHelper.ComputeHmacSha256(payload, "secret_A");
            string header = $"sha256={hash}";

            bool isValid = WhatsAppSecurityHelper.VerifyWebhookSignature(header, payload, "secret_B");
            Assert.False(isValid);
        }

        [Theory]
        [InlineData("invalid_prefix_without_sha256")]
        [InlineData("sha1=abcdef123456")]
        [InlineData("")]
        [InlineData(null)]
        public void VerifyWebhookSignature_InvalidHeaderFormat_ReturnsFalse(string? header)
        {
            string payload = "{\"entry\":[{\"id\":\"12345\"}]}";
            string secret = "secret";

            bool isValid = WhatsAppSecurityHelper.VerifyWebhookSignature(header, payload, secret);
            Assert.False(isValid);
        }

        [Fact]
        public void ConstantTimeEquals_ComparesCorrectly()
        {
            Assert.True(WhatsAppSecurityHelper.ConstantTimeEquals("abcdef", "abcdef"));
            Assert.False(WhatsAppSecurityHelper.ConstantTimeEquals("abcdef", "abcdeg"));
            Assert.False(WhatsAppSecurityHelper.ConstantTimeEquals("abcdef", "abcd"));
            Assert.False(WhatsAppSecurityHelper.ConstantTimeEquals(null, "abcdef"));
            Assert.True(WhatsAppSecurityHelper.ConstantTimeEquals(null, null));
        }

        [Fact]
        public void TryVerifyWebhookChallenge_ValidChallenge_Succeeds()
        {
            string mode = "subscribe";
            string token = "my_verify_token_777";
            string challenge = "1158201444";
            string configuredToken = "my_verify_token_777";

            bool success = WhatsAppSecurityHelper.TryVerifyWebhookChallenge(
                mode, token, challenge, configuredToken, out string? response);

            Assert.True(success);
            Assert.Equal("1158201444", response);
        }

        [Fact]
        public void TryVerifyWebhookChallenge_InvalidToken_Fails()
        {
            string mode = "subscribe";
            string token = "wrong_token";
            string challenge = "1158201444";
            string configuredToken = "my_verify_token_777";

            bool success = WhatsAppSecurityHelper.TryVerifyWebhookChallenge(
                mode, token, challenge, configuredToken, out string? response);

            Assert.False(success);
            Assert.Null(response);
        }

        [Fact]
        public void TryVerifyWebhookChallenge_InvalidMode_Fails()
        {
            string mode = "unsubscribe";
            string token = "my_verify_token_777";
            string challenge = "1158201444";
            string configuredToken = "my_verify_token_777";

            bool success = WhatsAppSecurityHelper.TryVerifyWebhookChallenge(
                mode, token, challenge, configuredToken, out string? response);

            Assert.False(success);
            Assert.Null(response);
        }
    }
}
