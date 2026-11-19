using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Sops.Configuration;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Sops.Database.Configuration;
using Xunit;

namespace Sops.Database.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void DatabaseSettings_DefaultValues_AreSensible()
        {
            var settings = new DatabaseSettings();

            Assert.Equal(string.Empty, settings.Provider);
            Assert.Equal(string.Empty, settings.ConnectionString);
            Assert.Equal(15, settings.ConnectionTimeoutSeconds);
            Assert.Equal(30, settings.CommandTimeoutSeconds);
            Assert.False(settings.ReadOnly);
            Assert.Null(settings.TenantId);
            Assert.NotNull(settings.Pooling);
            Assert.True(settings.Pooling.Enabled);
            Assert.Equal(0, settings.Pooling.MinPoolSize);
            Assert.Equal(100, settings.Pooling.MaxPoolSize);
            Assert.NotNull(settings.Retry);
            Assert.True(settings.Retry.Enabled);
            Assert.Equal(3, settings.Retry.MaxAttempts);
            Assert.Equal(200, settings.Retry.InitialDelayMilliseconds);
            Assert.Equal(2000, settings.Retry.MaxDelayMilliseconds);
            Assert.Equal(2.0, settings.Retry.BackoffMultiplier);
        }

        [Fact]
        public void DatabaseSettings_BindsFromConfiguration_Correctly()
        {
            var inMemoryData = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSql",
                ["Database:ConnectionString"] = "Host=localhost;Database=sops;Username=user;Password=pass",
                ["Database:ConnectionTimeoutSeconds"] = "20",
                ["Database:CommandTimeoutSeconds"] = "45",
                ["Database:ReadOnly"] = "true",
                ["Database:TenantId"] = "tenant-xyz",
                ["Database:Pooling:Enabled"] = "false",
                ["Database:Retry:MaxAttempts"] = "5"
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemoryData)
                .Build();

            var settings = new DatabaseSettings();
            config.GetSection("Database").Bind(settings);

            Assert.Equal("PostgreSql", settings.Provider);
            Assert.Equal("Host=localhost;Database=sops;Username=user;Password=pass", settings.ConnectionString);
            Assert.Equal(20, settings.ConnectionTimeoutSeconds);
            Assert.Equal(45, settings.CommandTimeoutSeconds);
            Assert.True(settings.ReadOnly);
            Assert.Equal("tenant-xyz", settings.TenantId);
            Assert.False(settings.Pooling.Enabled);
            Assert.Equal(5, settings.Retry.MaxAttempts);
        }

        [Fact]
        public void DatabaseSettings_MissingProvider_FailsValidation()
        {
            var settings = new DatabaseSettings
            {
                Provider = "",
                ConnectionString = "Server=localhost;"
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains("Provider", ex.Message);
        }

        [Fact]
        public void DatabaseSettings_InvalidMinPoolSize_FailsValidation()
        {
            var settings = new DatabaseSettings
            {
                Provider = "PostgreSql",
                Pooling = new DatabasePoolingSettings
                {
                    MinPoolSize = 50,
                    MaxPoolSize = 10
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains("MinPoolSize", ex.Message);
        }

        [Fact]
        public void DatabaseSettings_InvalidRetryDelayRange_FailsValidation()
        {
            var settings = new DatabaseSettings
            {
                Provider = "PostgreSql",
                Retry = new DatabaseRetrySettings
                {
                    InitialDelayMilliseconds = 5000,
                    MaxDelayMilliseconds = 1000
                }
            };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains("InitialDelayMilliseconds", ex.Message);
        }

        [Fact]
        public void DatabaseSettings_ConnectionString_HasSensitiveAttribute()
        {
            PropertyInfo? prop = typeof(DatabaseSettings).GetProperty(nameof(DatabaseSettings.ConnectionString));
            Assert.NotNull(prop);

            var sensitiveAttr = prop.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(sensitiveAttr);
        }

        [Fact]
        public void DatabaseSettings_ToSafeSummary_MasksConnectionString()
        {
            var settings = new DatabaseSettings
            {
                Provider = "SqlServer",
                ConnectionString = "Server=myServer;Database=myDb;User Id=myUser;Password=myPassword;",
                TenantId = "tenant-007",
                ConnectionTimeoutSeconds = 10,
                CommandTimeoutSeconds = 25
            };

            string summary = settings.ToSafeSummary();

            Assert.Contains("Provider='SqlServer'", summary);
            Assert.Contains("Tenant='tenant-007'", summary);
            Assert.Contains("ConnectionString=[REDACTED]", summary);
            Assert.DoesNotContain("myPassword", summary);
            Assert.DoesNotContain("myUser", summary);
        }
    }
}
