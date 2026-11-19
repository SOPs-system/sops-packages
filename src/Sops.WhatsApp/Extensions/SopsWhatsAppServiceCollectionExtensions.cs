using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sops.Configuration.Validation;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Providers.MetaCloud;
using Sops.WhatsApp.Registration;

namespace Sops.WhatsApp.Extensions
{
    /// <summary>
    /// Service collection extension methods for registering WhatsApp infrastructure and client providers.
    /// </summary>
    public static class SopsWhatsAppServiceCollectionExtensions
    {
        /// <summary>
        /// Registers WhatsApp messaging infrastructure bound to the specified configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root configuration.</param>
        /// <param name="sectionName">The configuration section name. Defaults to "WhatsApp".</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsWhatsApp(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = WhatsAppSettings.SectionName)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            IConfigurationSection section = configuration.GetSection(sectionName);
            var settings = new WhatsAppSettings();
            section.Bind(settings);

            // Fail fast at startup if configuration is invalid
            ConfigurationValidator.Validate(settings);

            services.Configure<WhatsAppSettings>(section);
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers WhatsApp messaging infrastructure with custom settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The configured WhatsApp settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsWhatsApp(
            this IServiceCollection services,
            WhatsAppSettings settings)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers WhatsApp messaging infrastructure using an action delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Delegate to configure WhatsApp settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsWhatsApp(
            this IServiceCollection services,
            Action<WhatsAppSettings> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var settings = new WhatsAppSettings();
            configure(settings);
            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers a custom WhatsApp client provider factory in the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="factory">The factory delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWhatsAppClient(
            this IServiceCollection services,
            string providerName,
            Func<IServiceProvider?, IWhatsAppClient> factory)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            services.Configure<WhatsAppClientRegistryOptions>(options =>
            {
                options.CustomRegistrations[providerName] = factory;
            });

            return services;
        }

        private static IServiceCollection RegisterCoreServices(IServiceCollection services)
        {
            services.TryAddSingleton<IWhatsAppClientRegistry>(sp =>
            {
                var registry = new WhatsAppClientRegistry();
                var options = sp.GetService<IOptions<WhatsAppClientRegistryOptions>>();
                if (options?.Value != null)
                {
                    foreach (var pair in options.Value.CustomRegistrations)
                    {
                        registry.Register(pair.Key, pair.Value);
                    }
                }
                return registry;
            });

            services.TryAddTransient<MetaCloudWhatsAppClient>();

            services.TryAddTransient<IWhatsAppClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<WhatsAppSettings>>().Value;
                var registry = sp.GetRequiredService<IWhatsAppClientRegistry>();
                return registry.CreateClient(settings.Provider, sp);
            });

            return services;
        }
    }

    /// <summary>
    /// Internal options container for custom WhatsApp client provider registrations.
    /// </summary>
    internal sealed class WhatsAppClientRegistryOptions
    {
        public Dictionary<string, Func<IServiceProvider?, IWhatsAppClient>> CustomRegistrations { get; } =
            new Dictionary<string, Func<IServiceProvider?, IWhatsAppClient>>(StringComparer.OrdinalIgnoreCase);
    }
}
