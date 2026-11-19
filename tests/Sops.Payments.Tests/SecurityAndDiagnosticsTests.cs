using System.Reflection;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Sops.Payments.Configuration;
using Sops.Payments.Models;
using Xunit;

namespace Sops.Payments.Tests
{
    public class SecurityAndDiagnosticsTests
    {
        [Fact]
        public void PaymobCredentials_HaveSensitiveAttribute()
        {
            AssertHasSensitiveAttribute(nameof(PaymobSettings.SecretKey));
            AssertHasSensitiveAttribute(nameof(PaymobSettings.ApiKey));
            AssertHasSensitiveAttribute(nameof(PaymobSettings.HmacSecret));
        }

        private static void AssertHasSensitiveAttribute(string propertyName)
        {
            var prop = typeof(PaymobSettings).GetProperty(propertyName);
            Assert.NotNull(prop);
            var attr = prop!.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void SecretMasker_RedactsPaymobSecrets()
        {
            var settings = new PaymobSettings
            {
                SecretKey = "sec_live_top_secret",
                ApiKey = "api_key_secret_123",
                HmacSecret = "hmac_secret_super_confidential"
            };

            var propSecretKey = typeof(PaymobSettings).GetProperty(nameof(PaymobSettings.SecretKey));
            var propApiKey = typeof(PaymobSettings).GetProperty(nameof(PaymobSettings.ApiKey));
            var propHmac = typeof(PaymobSettings).GetProperty(nameof(PaymobSettings.HmacSecret));

            Assert.True(SecretMasker.IsSensitive(propSecretKey!));
            Assert.True(SecretMasker.IsSensitive(propApiKey!));
            Assert.True(SecretMasker.IsSensitive(propHmac!));

            string logMessage = $"Failed connecting with key {settings.SecretKey} and {settings.HmacSecret}";
            string sanitized = SecretMasker.RedactValueFromMessage(logMessage, settings.SecretKey);
            sanitized = SecretMasker.RedactValueFromMessage(sanitized, settings.HmacSecret);

            Assert.DoesNotContain(settings.SecretKey, sanitized);
            Assert.DoesNotContain(settings.HmacSecret, sanitized);
            Assert.Contains(SecretMasker.RedactedPlaceholder, sanitized);
        }

        [Fact]
        public void MaskedPan_DoesNotExposeFullCreditCardNumber()
        {
            var result = PaymentVerificationResult.Success(
                "TX-1",
                "ORD-1",
                Money.FromDecimal(100m, "EGP"),
                maskedPan: "411111xxxxxx1111");

            Assert.NotNull(result.MaskedPan);
            Assert.Contains("x", result.MaskedPan);
            Assert.True(result.MaskedPan!.Length <= 19);
        }
    }
}
