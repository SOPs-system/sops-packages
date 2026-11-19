using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Sops.Cache.Configuration;
using Sops.Configuration.Validation;
using Xunit;

namespace Sops.Cache.Tests
{
    public class CacheSettingsTests
    {
        [Fact]
        public void DefaultValues_AreSensible()
        {
            var settings = new CacheSettings();

            Assert.Equal("Memory", settings.Provider);
            Assert.Null(settings.KeyPrefix);
            Assert.Equal(300, settings.DefaultTtlSeconds);
            Assert.Equal(300, settings.GetDefaultTtl().TotalSeconds);
        }

        [Fact]
        public void BindsFromConfiguration_Correctly()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Cache:Provider"] = "Redis",
                ["Cache:KeyPrefix"] = "app-prod",
                ["Cache:DefaultTtlSeconds"] = "1800"
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            var settings = new CacheSettings();
            config.GetSection("Cache").Bind(settings);

            Assert.Equal("Redis", settings.Provider);
            Assert.Equal("app-prod", settings.KeyPrefix);
            Assert.Equal(1800, settings.DefaultTtlSeconds);
        }

        [Fact]
        public void MissingProvider_FailsValidation()
        {
            var settings = new CacheSettings { Provider = "" };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains("Provider", ex.Message);
        }

        [Fact]
        public void InvalidTtl_FailsValidation()
        {
            var settings = new CacheSettings { DefaultTtlSeconds = -5 };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains("DefaultTtlSeconds", ex.Message);
        }

        [Fact]
        public void ToSafeSummary_FormatsCleanly()
        {
            var settings = new CacheSettings
            {
                Provider = "Redis",
                KeyPrefix = "sops-core",
                DefaultTtlSeconds = 600
            };

            string summary = settings.ToSafeSummary();

            Assert.Contains("Provider='Redis'", summary);
            Assert.Contains("KeyPrefix='sops-core'", summary);
            Assert.Contains("DefaultTtl=600s", summary);
        }
    }
}
