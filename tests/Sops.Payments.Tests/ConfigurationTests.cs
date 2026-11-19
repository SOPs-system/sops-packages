using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Sops.Configuration;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Sops.Payments.Configuration;
using Xunit;

namespace Sops.Payments.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void DefaultSettings_WithApiKey_AreValid()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    ApiKey = "test-api-key",
                    IntegrationId = 1234
                }
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.True(isValid);
            Assert.Empty(results);
            Assert.Equal("Paymob", settings.Provider);
            Assert.True(settings.Enabled);
            Assert.Equal("https://accept.paymob.com", settings.Paymob.BaseUrl);
            Assert.Equal(30, settings.Paymob.TimeoutSeconds);
            Assert.Equal("EGP", settings.Paymob.DefaultCurrency);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void InvalidProvider_FailsValidation(string? provider)
        {
            var settings = new PaymentSettings
            {
                Provider = provider!
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(PaymentSettings.Provider));
        }

        [Fact]
        public void MissingBothSecretKeyAndApiKey_FailsValidation()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = null,
                    ApiKey = null
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath.Contains(nameof(PaymobSettings.SecretKey)));
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("ftp://invalid-scheme.com")]
        public void InvalidBaseUrl_FailsValidation(string invalidUrl)
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    BaseUrl = invalidUrl,
                    SecretKey = "sec_key"
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(PaymobSettings.BaseUrl));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void InvalidTimeout_FailsValidation(int timeout)
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_key",
                    TimeoutSeconds = timeout
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(PaymobSettings.TimeoutSeconds));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("EG")]
        [InlineData("EGPP")]
        public void InvalidDefaultCurrency_FailsValidation(string? currency)
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_key",
                    DefaultCurrency = currency!
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(PaymobSettings.DefaultCurrency));
        }

        [Fact]
        public void SensitiveProperties_HaveSensitiveAttribute()
        {
            AssertPropertyHasSensitiveAttribute(typeof(PaymobSettings), nameof(PaymobSettings.SecretKey));
            AssertPropertyHasSensitiveAttribute(typeof(PaymobSettings), nameof(PaymobSettings.ApiKey));
            AssertPropertyHasSensitiveAttribute(typeof(PaymobSettings), nameof(PaymobSettings.HmacSecret));
        }

        private static void AssertPropertyHasSensitiveAttribute(System.Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName);
            Assert.NotNull(prop);
            var attr = prop!.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void ConfigurationBinding_FromInMemory_BindsAllProperties()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Payment:Provider"] = "Paymob",
                ["Payment:Enabled"] = "true",
                ["Payment:Paymob:BaseUrl"] = "https://accept.paymob.com",
                ["Payment:Paymob:SecretKey"] = "sec_test_secret_123",
                ["Payment:Paymob:PublicKey"] = "sec_test_public_456",
                ["Payment:Paymob:ApiKey"] = "test_api_key_789",
                ["Payment:Paymob:HmacSecret"] = "hmac_secret_xyz",
                ["Payment:Paymob:IntegrationId"] = "987654",
                ["Payment:Paymob:IframeId"] = "12345",
                ["Payment:Paymob:DefaultCurrency"] = "EGP",
                ["Payment:Paymob:TimeoutSeconds"] = "45",
                ["Payment:Paymob:NotificationUrl"] = "https://api.example.com/paymob-callback",
                ["Payment:Paymob:RedirectionUrl"] = "https://example.com/checkout/return"
            };

            var configuration = SopsConfiguration.CreateBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            var settings = new PaymentSettings();
            configuration.GetSection(PaymentSettings.SectionName).Bind(settings);

            Assert.Equal("Paymob", settings.Provider);
            Assert.True(settings.Enabled);
            Assert.Equal("https://accept.paymob.com", settings.Paymob.BaseUrl);
            Assert.Equal("sec_test_secret_123", settings.Paymob.SecretKey);
            Assert.Equal("sec_test_public_456", settings.Paymob.PublicKey);
            Assert.Equal("test_api_key_789", settings.Paymob.ApiKey);
            Assert.Equal("hmac_secret_xyz", settings.Paymob.HmacSecret);
            Assert.Equal(987654, settings.Paymob.IntegrationId);
            Assert.Equal(12345, settings.Paymob.IframeId);
            Assert.Equal("EGP", settings.Paymob.DefaultCurrency);
            Assert.Equal(45, settings.Paymob.TimeoutSeconds);
            Assert.Equal("https://api.example.com/paymob-callback", settings.Paymob.NotificationUrl);
            Assert.Equal("https://example.com/checkout/return", settings.Paymob.RedirectionUrl);
        }
    }
}
