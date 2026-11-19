using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Mail.Abstractions;
using Sops.Mail.Configuration;
using Sops.Mail.Providers;

namespace Sops.Mail.Registration
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IMailProviderRegistry"/> supporting pluggable mail transport providers.
    /// Pre-configures the standard "Smtp" provider.
    /// </summary>
    public class MailProviderRegistry : IMailProviderRegistry
    {
        private readonly ConcurrentDictionary<string, Func<IServiceProvider?, IMailSender>> _factories =
            new ConcurrentDictionary<string, Func<IServiceProvider?, IMailSender>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="MailProviderRegistry"/> class with default providers.
        /// </summary>
        public MailProviderRegistry()
        {
            Register("Smtp", sp =>
            {
                if (sp != null)
                {
                    var options = sp.GetRequiredService<IOptions<MailSettings>>();
                    var logger = sp.GetService<ILogger<SmtpMailSender>>();
                    var clientFactory = sp.GetService<ISmtpClientFactory>();
                    return new SmtpMailSender(options, logger, clientFactory);
                }

                return new SmtpMailSender(new MailSettings());
            });
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> RegisteredProviders => _factories.Keys.ToList();

        /// <inheritdoc />
        public void Register(string providerName, Func<IServiceProvider?, IMailSender> factory)
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
        public IMailSender CreateProvider(string providerName, IServiceProvider? serviceProvider = null)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            if (!_factories.TryGetValue(providerName, out var factory))
            {
                string available = string.Join(", ", _factories.Keys);
                throw new NotSupportedException(
                    $"Mail provider '{providerName}' is not registered. Registered providers are: [{available}].");
            }

            return factory(serviceProvider);
        }
    }
}
