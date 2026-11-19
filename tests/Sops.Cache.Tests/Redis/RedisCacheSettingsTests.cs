using System.Reflection;
using Sops.Cache.Redis.Configuration;
using Sops.Configuration.Models;
using Sops.Configuration.Validation;
using Xunit;

namespace Sops.Cache.Tests.Redis
{
    public class RedisCacheSettingsTests
    {
        [Fact]
        public void ConnectionString_HasSensitiveAttribute()
        {
            PropertyInfo? prop = typeof(RedisCacheSettings).GetProperty(nameof(RedisCacheSettings.ConnectionString));
            Assert.NotNull(prop);

            var attr = prop.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void DefaultValues_AreSensible()
        {
            var settings = new RedisCacheSettings();

            Assert.Equal(string.Empty, settings.ConnectionString);
            Assert.Equal(0, settings.Database);
            Assert.Equal(5000, settings.ConnectTimeoutMilliseconds);
            Assert.Equal(5000, settings.SyncTimeoutMilliseconds);
            Assert.False(settings.AbortOnConnectFail);
        }

        [Fact]
        public void MissingConnectionString_FailsValidation()
        {
            var settings = new RedisCacheSettings { ConnectionString = "" };

            var ex = Assert.Throws<ConfigurationValidationException>(() =>
                ConfigurationValidator.Validate(settings));

            Assert.Contains("ConnectionString", ex.Message);
        }

        [Fact]
        public void ToSafeSummary_MasksConnectionString()
        {
            var settings = new RedisCacheSettings
            {
                ConnectionString = "redis-host:6379,password=TopSecretToken123!",
                Database = 2
            };

            string summary = settings.ToSafeSummary();

            Assert.Contains("RedisDatabase=2", summary);
            Assert.Contains("Connection=[REDACTED]", summary);
            Assert.DoesNotContain("TopSecretToken123!", summary);
        }
    }
}
