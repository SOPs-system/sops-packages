using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Sops.Configuration;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Sops.Mail.Configuration;
using Xunit;

namespace Sops.Mail.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void DefaultSettings_AreValid()
        {
            var settings = new MailSettings
            {
                FromAddress = "noreply@sops.internal"
            };

            var context = new ValidationContext(settings);
            var results = new System.Collections.Generic.List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(settings, context, results, true);

            Assert.True(isValid);
            Assert.Empty(results);
            Assert.Equal("Smtp", settings.Provider);
            Assert.True(settings.Enabled);
            Assert.Equal("localhost", settings.Smtp.Host);
            Assert.Equal(587, settings.Smtp.Port);
            Assert.Equal(SmtpSecurity.Auto, settings.Smtp.Security);
            Assert.Equal(30, settings.Smtp.TimeoutSeconds);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void InvalidProvider_FailsValidation(string? provider)
        {
            var settings = new MailSettings
            {
                Provider = provider!
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(MailSettings.Provider));
        }

        [Theory]
        [InlineData("invalid-email")]
        [InlineData("user@")]
        [InlineData("@domain.com")]
        public void InvalidFromAddress_FailsValidation(string invalidAddress)
        {
            var settings = new MailSettings
            {
                FromAddress = invalidAddress
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(MailSettings.FromAddress));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void InvalidSmtpHost_FailsValidation(string? host)
        {
            var settings = new MailSettings
            {
                Smtp = new SmtpSettings { Host = host! }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(SmtpSettings.Host));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        [InlineData(100000)]
        public void InvalidSmtpPort_FailsValidation(int port)
        {
            var settings = new MailSettings
            {
                Smtp = new SmtpSettings { Port = port }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(SmtpSettings.Port));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void InvalidTimeout_FailsValidation(int timeout)
        {
            var settings = new MailSettings
            {
                Smtp = new SmtpSettings { TimeoutSeconds = timeout }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(SmtpSettings.TimeoutSeconds));
        }

        [Fact]
        public void RequireAuthentication_WithoutUsername_FailsValidation()
        {
            var settings = new MailSettings
            {
                Smtp = new SmtpSettings
                {
                    RequireAuthentication = true,
                    Username = null
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains(ex.Errors, e => e.PropertyPath == nameof(SmtpSettings.Username));
        }

        [Fact]
        public void SmtpPassword_IsDecoratedWithSensitiveAttribute()
        {
            var prop = typeof(SmtpSettings).GetProperty(nameof(SmtpSettings.Password));
            Assert.NotNull(prop);

            var attr = prop.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void ConfigurationBinding_FromInMemory_BindsCorrectly()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Mail:Provider"] = "Smtp",
                ["Mail:Enabled"] = "true",
                ["Mail:FromAddress"] = "alerts@sops.internal",
                ["Mail:FromName"] = "SOPs Monitoring",
                ["Mail:Smtp:Host"] = "smtp.mailgun.org",
                ["Mail:Smtp:Port"] = "465",
                ["Mail:Smtp:Security"] = "SslOnConnect",
                ["Mail:Smtp:Username"] = "mailgun-user",
                ["Mail:Smtp:Password"] = "super-secret-key",
                ["Mail:Smtp:TimeoutSeconds"] = "45",
                ["Mail:Smtp:RequireAuthentication"] = "true"
            };

            var configuration = SopsConfiguration.CreateBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            var settings = new MailSettings();
            configuration.GetSection(MailSettings.SectionName).Bind(settings);

            Assert.Equal("Smtp", settings.Provider);
            Assert.True(settings.Enabled);
            Assert.Equal("alerts@sops.internal", settings.FromAddress);
            Assert.Equal("SOPs Monitoring", settings.FromName);
            Assert.Equal("smtp.mailgun.org", settings.Smtp.Host);
            Assert.Equal(465, settings.Smtp.Port);
            Assert.Equal(SmtpSecurity.SslOnConnect, settings.Smtp.Security);
            Assert.Equal("mailgun-user", settings.Smtp.Username);
            Assert.Equal("super-secret-key", settings.Smtp.Password);
            Assert.Equal(45, settings.Smtp.TimeoutSeconds);
            Assert.True(settings.Smtp.RequireAuthentication);
        }
    }
}
