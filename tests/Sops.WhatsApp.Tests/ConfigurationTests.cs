using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Extensions;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void ValidSettings_PassesValidation()
        {
            var settings = new WhatsAppSettings
            {
                Provider = "MetaCloud",
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                GraphApiVersion = "v21.0",
                PhoneNumberId = "102553952458",
                AccessToken = "EAAB123456789",
                AppSecret = "secret123",
                WebhookVerifyToken = "token123",
                TimeoutSeconds = 45,
                DefaultLanguage = "en_US"
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Fact]
        public void DisabledSettings_SkipsCredentialValidation()
        {
            var settings = new WhatsAppSettings
            {
                Enabled = false,
                BaseUrl = "",
                PhoneNumberId = "",
                AccessToken = ""
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("", "BaseUrl")]
        [InlineData("ftp://graph.facebook.com", "BaseUrl")]
        [InlineData("not-a-url", "BaseUrl")]
        public void InvalidBaseUrl_WhenEnabled_FailsValidation(string invalidUrl, string memberName)
        {
            var settings = new WhatsAppSettings
            {
                Enabled = true,
                BaseUrl = invalidUrl,
                PhoneNumberId = "102553952458",
                AccessToken = "EAAB123456789"
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(memberName));
        }

        [Fact]
        public void MissingPhoneNumberId_WhenEnabled_FailsValidation()
        {
            var settings = new WhatsAppSettings
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                PhoneNumberId = null,
                AccessToken = "EAAB123456789"
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(WhatsAppSettings.PhoneNumberId)));
        }

        [Fact]
        public void MissingAccessToken_WhenEnabled_FailsValidation()
        {
            var settings = new WhatsAppSettings
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                PhoneNumberId = "102553952458",
                AccessToken = " "
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(WhatsAppSettings.AccessToken)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(301)]
        public void InvalidTimeout_FailsValidation(int invalidTimeout)
        {
            var settings = new WhatsAppSettings
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                PhoneNumberId = "102553952458",
                AccessToken = "EAAB123456789",
                TimeoutSeconds = invalidTimeout
            };

            var context = new ValidationContext(settings);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(WhatsAppSettings.TimeoutSeconds)));
        }

        [Fact]
        public void SensitiveAttributes_PresentOnSecretProperties()
        {
            var type = typeof(WhatsAppSettings);

            var accessTokenProp = type.GetProperty(nameof(WhatsAppSettings.AccessToken));
            var appSecretProp = type.GetProperty(nameof(WhatsAppSettings.AppSecret));
            var verifyTokenProp = type.GetProperty(nameof(WhatsAppSettings.WebhookVerifyToken));

            Assert.NotNull(accessTokenProp?.GetCustomAttribute<SensitiveAttribute>());
            Assert.NotNull(appSecretProp?.GetCustomAttribute<SensitiveAttribute>());
            Assert.NotNull(verifyTokenProp?.GetCustomAttribute<SensitiveAttribute>());
        }

        [Fact]
        public void AddSopsWhatsApp_ConfigurationBinding_Succeeds()
        {
            var configData = new Dictionary<string, string?>
            {
                ["WhatsApp:Provider"] = "MetaCloud",
                ["WhatsApp:Enabled"] = "true",
                ["WhatsApp:BaseUrl"] = "https://graph.facebook.com",
                ["WhatsApp:GraphApiVersion"] = "v21.0",
                ["WhatsApp:PhoneNumberId"] = "987654321",
                ["WhatsApp:AccessToken"] = "token_xyz",
                ["WhatsApp:AppSecret"] = "secret_xyz",
                ["WhatsApp:WebhookVerifyToken"] = "verify_xyz",
                ["WhatsApp:TimeoutSeconds"] = "25",
                ["WhatsApp:DefaultLanguage"] = "ar"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsWhatsApp(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<WhatsAppSettings>>().Value;

            Assert.Equal("MetaCloud", options.Provider);
            Assert.True(options.Enabled);
            Assert.Equal("987654321", options.PhoneNumberId);
            Assert.Equal("token_xyz", options.AccessToken);
            Assert.Equal("secret_xyz", options.AppSecret);
            Assert.Equal(25, options.TimeoutSeconds);
            Assert.Equal("ar", options.DefaultLanguage);
        }

        [Fact]
        public void AddSopsWhatsApp_InvalidConfig_ThrowsValidationException()
        {
            var configData = new Dictionary<string, string?>
            {
                ["WhatsApp:Enabled"] = "true",
                ["WhatsApp:PhoneNumberId"] = "" // Missing required phone number id
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            Assert.Throws<ConfigurationValidationException>(() => services.AddSopsWhatsApp(configuration));
        }
    }
}
