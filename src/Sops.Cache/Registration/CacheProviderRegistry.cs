using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sops.Cache.Abstractions;

namespace Sops.Cache.Registration
{
    /// <summary>
    /// Thread-safe implementation of <see cref="ICacheProviderRegistry"/>.
    /// Supports dynamic registration of memory, Redis, and custom cache providers.
    /// </summary>
    public class CacheProviderRegistry : ICacheProviderRegistry
    {
        private readonly ConcurrentDictionary<string, Func<IServiceProvider?, ICacheService>> _factories =
            new ConcurrentDictionary<string, Func<IServiceProvider?, ICacheService>>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IReadOnlyCollection<string> RegisteredProviders => _factories.Keys.ToList();

        /// <inheritdoc />
        public void Register(string providerName, Func<IServiceProvider?, ICacheService> factory)
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
        public ICacheService CreateProvider(string providerName, IServiceProvider? serviceProvider = null)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            if (!_factories.TryGetValue(providerName, out var factory))
            {
                string available = string.Join(", ", _factories.Keys);
                throw new NotSupportedException(
                    $"Cache provider '{providerName}' is not registered. Registered providers are: [{available}].");
            }

            return factory(serviceProvider);
        }
    }
}
