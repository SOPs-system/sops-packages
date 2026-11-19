using System;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Documents;
using Sops.Database.Registration;
using Sops.Database.Tests.TestHelpers;
using Xunit;

namespace Sops.Database.Tests
{
    public class ProviderRegistrationTests
    {
        [Fact]
        public void Registry_ContainsBuiltInDocumentProvider()
        {
            var registry = new DatabaseProviderRegistry();

            Assert.True(registry.IsRegistered("InMemoryDocument"));
            Assert.True(registry.IsRegistered("inmemorydocument"));

            var settings = new DatabaseSettings { Provider = "InMemoryDocument" };
            IDatabaseProvider provider = registry.CreateProvider(settings);

            Assert.NotNull(provider);
            Assert.IsType<InMemoryDocumentDatabaseProvider>(provider);
            Assert.Equal("InMemoryDocument", provider.ProviderName);
        }

        [Fact]
        public void Registry_CanRegisterAndResolveCustomProvider()
        {
            var registry = new DatabaseProviderRegistry();
            registry.Register("Sqlite", (settings, _) => new SqliteTestDatabaseProvider(settings));

            Assert.True(registry.IsRegistered("Sqlite"));

            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = "Data Source=:memory:"
            };

            IDatabaseProvider provider = registry.CreateProvider(settings);

            Assert.NotNull(provider);
            Assert.Equal("Sqlite", provider.ProviderName);
            Assert.True(provider.Capabilities.SupportsRelationalCommands);
        }

        [Fact]
        public void Registry_UnregisteredProvider_ThrowsNotSupportedException()
        {
            var registry = new DatabaseProviderRegistry();

            var settings = new DatabaseSettings
            {
                Provider = "UnknownOracleProvider"
            };

            var ex = Assert.Throws<NotSupportedException>(() => registry.CreateProvider(settings));

            Assert.Contains("UnknownOracleProvider", ex.Message);
            Assert.Contains("is not registered", ex.Message);
        }

        [Fact]
        public void Registry_MissingProviderSetting_ThrowsInvalidOperationException()
        {
            var registry = new DatabaseProviderRegistry();
            var settings = new DatabaseSettings { Provider = "" };

            Assert.Throws<InvalidOperationException>(() => registry.CreateProvider(settings));
        }

        [Fact]
        public void Registry_RegisterNullOrEmpty_ThrowsArgumentException()
        {
            var registry = new DatabaseProviderRegistry();

            Assert.Throws<ArgumentException>(() => registry.Register("", (s, sp) => null!));
            Assert.Throws<ArgumentNullException>(() => registry.Register("ValidName", null!));
        }
    }
}
