using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Documents;
using Sops.Database.Extensions;
using Sops.Database.Registration;
using Sops.Database.Relational;
using Sops.Database.Tests.TestHelpers;
using Xunit;

namespace Sops.Database.Tests
{
    public class CompatibilityTests
    {
        [Fact]
        public void DependencyInjection_RegistersAndResolvesRelationalProvider()
        {
            var configData = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = "Data Source=:memory:",
                ["Database:ConnectionTimeoutSeconds"] = "15"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsDatabase(configuration);
            services.AddDatabaseProvider("Sqlite", (settings, _) => new SqliteTestDatabaseProvider(settings));

            using var provider = services.BuildServiceProvider();

            var dbProvider = provider.GetRequiredService<IDatabaseProvider>();
            Assert.NotNull(dbProvider);
            Assert.Equal("Sqlite", dbProvider.ProviderName);

            var relational = provider.GetRequiredService<IRelationalDatabaseProvider>();
            Assert.NotNull(relational);
            Assert.Equal("Sqlite", relational.ProviderName);
        }

        [Fact]
        public void DependencyInjection_RegistersAndResolvesDocumentProvider()
        {
            var configData = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemoryDocument"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsDatabase(configuration);

            using var provider = services.BuildServiceProvider();

            var dbProvider = provider.GetRequiredService<IDatabaseProvider>();
            Assert.NotNull(dbProvider);
            Assert.Equal("InMemoryDocument", dbProvider.ProviderName);

            var docProvider = provider.GetRequiredService<IDocumentDatabaseProvider>();
            Assert.NotNull(docProvider);
            Assert.Equal("InMemoryDocument", docProvider.ProviderName);
        }

        [Fact]
        public void Standalone_UsageWithoutDependencyInjection_WorksCleanly()
        {
            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = "Data Source=:memory:"
            };

            var registry = new DatabaseProviderRegistry();
            registry.Register("Sqlite", (s, _) => new SqliteTestDatabaseProvider(s));

            using var db = (IRelationalDatabaseProvider)registry.CreateProvider(settings);
            Assert.NotNull(db);
            Assert.Equal("Sqlite", db.ProviderName);
        }

        [Fact]
        public void PublicApi_TargetCompatibility_InterfacesAreAccessible()
        {
            Assert.True(typeof(IDatabaseProvider).IsInterface);
            Assert.True(typeof(IRelationalDatabaseProvider).IsInterface);
            Assert.True(typeof(IDocumentDatabaseProvider).IsInterface);
            Assert.True(typeof(IRelationalTransaction).IsInterface);
            Assert.True(typeof(IDatabaseProviderRegistry).IsInterface);
            Assert.True(typeof(IDocumentCollection<>).IsInterface);
            Assert.True(typeof(IDocumentCollection<,>).IsInterface);
        }
    }
}
