using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sops.Configuration.Validation;
using Sops.Payments.Abstractions;
using Sops.Payments.Configuration;
using Sops.Payments.Providers.Paymob;
using Sops.Payments.Registration;

namespace Sops.Payments.Extensions
{
    /// <summary>
    /// Service collection extension methods for registering payment infrastructure and gateway providers.
    /// </summary>
    public static class SopsPaymentsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers payment infrastructure bound to the specified configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root configuration.</param>
        /// <param name="sectionName">The configuration section name. Defaults to "Payment".</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsPayments(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = PaymentSettings.SectionName)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            IConfigurationSection section = configuration.GetSection(sectionName);
            var settings = new PaymentSettings();
            section.Bind(settings);

            // Fail fast at startup if configuration is invalid
            ConfigurationValidator.Validate(settings);

            services.Configure<PaymentSettings>(section);
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers payment infrastructure with custom settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The configured payment settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsPayments(
            this IServiceCollection services,
            PaymentSettings settings)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers payment infrastructure using an action delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Delegate to configure payment settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsPayments(
            this IServiceCollection services,
            Action<PaymentSettings> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var settings = new PaymentSettings();
            configure(settings);
            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers a custom payment gateway provider factory in the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerName">The provider name (e.g. "Stripe", "Fawry").</param>
        /// <param name="factory">The factory delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPaymentGateway(
            this IServiceCollection services,
            string providerName,
            Func<IServiceProvider?, IPaymentGateway> factory)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            services.Configure<PaymentGatewayRegistryOptions>(options =>
            {
                options.CustomRegistrations[providerName] = factory;
            });

            return services;
        }

        private static IServiceCollection RegisterCoreServices(IServiceCollection services)
        {
            services.TryAddSingleton<IPaymentGatewayRegistry>(sp =>
            {
                var registry = new PaymentGatewayRegistry();
                var options = sp.GetService<IOptions<PaymentGatewayRegistryOptions>>();
                if (options?.Value != null)
                {
                    foreach (var pair in options.Value.CustomRegistrations)
                    {
                        registry.Register(pair.Key, pair.Value);
                    }
                }
                return registry;
            });

            services.TryAddTransient<PaymobPaymentGateway>();

            services.TryAddTransient<IPaymentGateway>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<PaymentSettings>>().Value;
                var registry = sp.GetRequiredService<IPaymentGatewayRegistry>();
                return registry.CreateGateway(settings.Provider, sp);
            });

            return services;
        }
    }

    /// <summary>
    /// Internal options container for custom payment gateway provider registrations.
    /// </summary>
    internal sealed class PaymentGatewayRegistryOptions
    {
        public Dictionary<string, Func<IServiceProvider?, IPaymentGateway>> CustomRegistrations { get; } =
            new Dictionary<string, Func<IServiceProvider?, IPaymentGateway>>(StringComparer.OrdinalIgnoreCase);
    }
}
