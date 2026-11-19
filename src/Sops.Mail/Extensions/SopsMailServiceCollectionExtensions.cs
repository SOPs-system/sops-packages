using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sops.Configuration.Validation;
using Sops.Mail.Abstractions;
using Sops.Mail.Configuration;
using Sops.Mail.Providers;
using Sops.Mail.Registration;

namespace Sops.Mail.Extensions
{
    /// <summary>
    /// Extension methods for setting up SOPs email infrastructure in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class SopsMailServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SOPs email services bound to the specified configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root configuration.</param>
        /// <param name="sectionName">The configuration section name. Defaults to "Mail".</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsMail(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = MailSettings.SectionName)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            IConfigurationSection section = configuration.GetSection(sectionName);
            var settings = new MailSettings();
            section.Bind(settings);

            // Startup validation: fail fast if settings are invalid
            ConfigurationValidator.Validate(settings);

            services.Configure<MailSettings>(section);
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers SOPs email services with pre-configured settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The mail settings instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsMail(
            this IServiceCollection services,
            MailSettings settings)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers SOPs email services using an action delegate to configure settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Delegate to configure mail settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsMail(
            this IServiceCollection services,
            Action<MailSettings> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var settings = new MailSettings();
            configure(settings);
            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers a custom mail provider factory.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerName">The provider name (e.g. "SendGrid", "AmazonSes").</param>
        /// <param name="factory">The factory method to construct the provider.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMailProvider(
            this IServiceCollection services,
            string providerName,
            Func<IServiceProvider?, IMailSender> factory)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            services.Configure<MailProviderRegistryOptions>(options =>
            {
                options.CustomRegistrations[providerName] = factory;
            });

            return services;
        }

        private static IServiceCollection RegisterCoreServices(IServiceCollection services)
        {
            services.TryAddSingleton<ISmtpClientFactory, DefaultSmtpClientFactory>();

            services.TryAddSingleton<IMailProviderRegistry>(sp =>
            {
                var registry = new MailProviderRegistry();
                var options = sp.GetService<IOptions<MailProviderRegistryOptions>>();
                if (options?.Value != null)
                {
                    foreach (var pair in options.Value.CustomRegistrations)
                    {
                        registry.Register(pair.Key, pair.Value);
                    }
                }
                return registry;
            });

            services.TryAddTransient<SmtpMailSender>();

            services.TryAddTransient<IMailSender>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<MailSettings>>().Value;
                var registry = sp.GetRequiredService<IMailProviderRegistry>();
                return registry.CreateProvider(settings.Provider, sp);
            });

            return services;
        }
    }

    /// <summary>
    /// Internal options container for custom mail provider registrations.
    /// </summary>
    internal sealed class MailProviderRegistryOptions
    {
        public Dictionary<string, Func<IServiceProvider?, IMailSender>> CustomRegistrations { get; } =
            new Dictionary<string, Func<IServiceProvider?, IMailSender>>(StringComparer.OrdinalIgnoreCase);
    }
}
