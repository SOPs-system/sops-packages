using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Documents;

namespace Sops.Database.Registration
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IDatabaseProviderRegistry"/>.
    /// Supports dynamic registration of relational and document database providers.
    /// </summary>
    public class DatabaseProviderRegistry : IDatabaseProviderRegistry
    {
        private readonly ConcurrentDictionary<string, Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider>> _factories =
            new ConcurrentDictionary<string, Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider>>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IReadOnlyCollection<string> RegisteredProviders => _factories.Keys.ToList();

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseProviderRegistry"/> class with default built-in providers.
        /// </summary>
        public DatabaseProviderRegistry()
        {
            // Register built-in in-memory document provider for testing and prototyping
            Register("InMemoryDocument", (settings, sp) =>
            {
                var logger = sp?.GetService<ILogger<InMemoryDocumentDatabaseProvider>>();
                return new InMemoryDocumentDatabaseProvider(settings, logger);
            });
        }

        /// <inheritdoc />
        public void Register(string providerName, Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider> factory)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factories[providerName] = factory;
        }

        /// <inheritdoc />
        public bool IsRegistered(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return false;
            return _factories.ContainsKey(providerName);
        }

        /// <inheritdoc />
        public IDatabaseProvider CreateProvider(DatabaseSettings settings, IServiceProvider? serviceProvider = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(settings.Provider))
            {
                throw new InvalidOperationException("Cannot create a database provider because DatabaseSettings.Provider is not specified.");
            }

            if (!_factories.TryGetValue(settings.Provider, out var factory))
            {
                string available = string.Join(", ", _factories.Keys);
                throw new NotSupportedException(
                    $"Database provider '{settings.Provider}' is not registered. Registered providers are: [{available}].");
            }

            return factory(settings, serviceProvider);
        }
    }
}
