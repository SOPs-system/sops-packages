using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sops.Configuration.Validation;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Documents;
using Sops.Database.Registration;
using Sops.Database.Relational;

namespace Sops.Database.Extensions
{
    /// <summary>
    /// Extension methods for setting up SOPs Database services in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class SopsDatabaseServiceCollectionExtensions
    {
        /// <summary>
        /// Registers database infrastructure, provider registry, and resolved database providers from configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root application configuration.</param>
        /// <param name="sectionName">The configuration section name. Defaults to "Database".</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsDatabase(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "Database")
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            IConfigurationSection section = configuration.GetSection(sectionName);
            var settings = new DatabaseSettings();
            section.Bind(settings);

            // Fail fast at startup if configuration is invalid
            ConfigurationValidator.Validate(settings);

            services.Configure<DatabaseSettings>(section);
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers database infrastructure, provider registry, and resolved database providers with custom settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The database settings instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsDatabase(
            this IServiceCollection services,
            DatabaseSettings settings)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers database infrastructure, provider registry, and resolved database providers with an action delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsDatabase(
            this IServiceCollection services,
            Action<DatabaseSettings> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var settings = new DatabaseSettings();
            configure(settings);
            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Adds a custom provider factory to the registry in the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="factory">The provider factory delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddDatabaseProvider(
            this IServiceCollection services,
            string providerName,
            Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider> factory)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            services.Configure<DatabaseProviderRegistryOptions>(options =>
            {
                options.CustomRegistrations[providerName] = factory;
            });

            return services;
        }

        private static IServiceCollection RegisterCoreServices(IServiceCollection services)
        {
            services.TryAddSingleton<IDatabaseProviderRegistry>(sp =>
            {
                var registry = new DatabaseProviderRegistry();
                var options = sp.GetService<IOptions<DatabaseProviderRegistryOptions>>();
                if (options?.Value != null)
                {
                    foreach (var pair in options.Value.CustomRegistrations)
                    {
                        registry.Register(pair.Key, pair.Value);
                    }
                }
                return registry;
            });

            // Register primary IDatabaseProvider resolved dynamically from DatabaseSettings.Provider
            services.TryAddTransient<IDatabaseProvider>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
                var registry = sp.GetRequiredService<IDatabaseProviderRegistry>();
                return registry.CreateProvider(settings, sp);
            });

            // Register IRelationalDatabaseProvider if the resolved provider is relational
            services.TryAddTransient<IRelationalDatabaseProvider>(sp =>
            {
                var provider = sp.GetRequiredService<IDatabaseProvider>();
                if (provider is IRelationalDatabaseProvider relational)
                {
                    return relational;
                }

                throw new InvalidOperationException(
                    $"Configured database provider '{provider.ProviderName}' does not implement IRelationalDatabaseProvider.");
            });

            // Register IDocumentDatabaseProvider if the resolved provider is document-based
            services.TryAddTransient<IDocumentDatabaseProvider>(sp =>
            {
                var provider = sp.GetRequiredService<IDatabaseProvider>();
                if (provider is IDocumentDatabaseProvider doc)
                {
                    return doc;
                }

                throw new InvalidOperationException(
                    $"Configured database provider '{provider.ProviderName}' does not implement IDocumentDatabaseProvider.");
            });

            return services;
        }
    }

    /// <summary>
    /// Internal options container for custom provider factory registrations.
    /// </summary>
    internal sealed class DatabaseProviderRegistryOptions
    {
        public System.Collections.Generic.Dictionary<string, Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider>> CustomRegistrations { get; } =
            new System.Collections.Generic.Dictionary<string, Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider>>(StringComparer.OrdinalIgnoreCase);
    }
}
