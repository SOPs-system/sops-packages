using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Providers.MetaCloud;

namespace Sops.WhatsApp.Registration
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IWhatsAppClientRegistry"/> supporting pluggable WhatsApp providers.
    /// Pre-configures the default "MetaCloud" provider.
    /// </summary>
    public class WhatsAppClientRegistry : IWhatsAppClientRegistry
    {
        private readonly ConcurrentDictionary<string, Func<IServiceProvider?, IWhatsAppClient>> _factories =
            new ConcurrentDictionary<string, Func<IServiceProvider?, IWhatsAppClient>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="WhatsAppClientRegistry"/> class with default "MetaCloud" provider.
        /// </summary>
        public WhatsAppClientRegistry()
        {
            Register("MetaCloud", sp =>
            {
                if (sp != null)
                {
                    var options = sp.GetRequiredService<IOptions<WhatsAppSettings>>();
                    var httpClient = sp.GetService<HttpClient>();
                    var logger = sp.GetService<ILogger<MetaCloudWhatsAppClient>>();
                    return new MetaCloudWhatsAppClient(options, httpClient, logger);
                }

                return new MetaCloudWhatsAppClient(new WhatsAppSettings());
            });
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> RegisteredProviders => _factories.Keys.ToList();

        /// <inheritdoc />
        public void Register(string providerName, Func<IServiceProvider?, IWhatsAppClient> factory)
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
        public IWhatsAppClient CreateClient(string providerName, IServiceProvider? serviceProvider = null)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            if (!_factories.TryGetValue(providerName, out var factory))
            {
                string available = string.Join(", ", _factories.Keys);
                throw new NotSupportedException(
                    $"WhatsApp provider '{providerName}' is not registered. Registered providers are: [{available}].");
            }

            return factory(serviceProvider);
        }
    }
}
