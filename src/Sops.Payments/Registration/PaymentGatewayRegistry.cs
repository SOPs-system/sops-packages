using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Payments.Abstractions;
using Sops.Payments.Configuration;
using Sops.Payments.Providers.Paymob;

namespace Sops.Payments.Registration
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IPaymentGatewayRegistry"/> supporting pluggable payment gateway backends.
    /// Pre-configures the default "Paymob" provider.
    /// </summary>
    public class PaymentGatewayRegistry : IPaymentGatewayRegistry
    {
        private readonly ConcurrentDictionary<string, Func<IServiceProvider?, IPaymentGateway>> _factories =
            new ConcurrentDictionary<string, Func<IServiceProvider?, IPaymentGateway>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentGatewayRegistry"/> class with default providers.
        /// </summary>
        public PaymentGatewayRegistry()
        {
            Register("Paymob", sp =>
            {
                if (sp != null)
                {
                    var options = sp.GetRequiredService<IOptions<PaymentSettings>>();
                    var httpClient = sp.GetService<HttpClient>();
                    var logger = sp.GetService<ILogger<PaymobPaymentGateway>>();
                    return new PaymobPaymentGateway(options, httpClient, logger);
                }

                return new PaymobPaymentGateway(new PaymentSettings());
            });
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> RegisteredProviders => _factories.Keys.ToList();

        /// <inheritdoc />
        public void Register(string providerName, Func<IServiceProvider?, IPaymentGateway> factory)
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
        public IPaymentGateway CreateGateway(string providerName, IServiceProvider? serviceProvider = null)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            if (!_factories.TryGetValue(providerName, out var factory))
            {
                string available = string.Join(", ", _factories.Keys);
                throw new NotSupportedException(
                    $"Payment provider '{providerName}' is not registered. Registered providers are: [{available}].");
            }

            return factory(serviceProvider);
        }
    }
}
