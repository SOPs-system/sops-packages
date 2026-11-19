using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Registration;
using Sops.Configuration.Validation;

namespace Sops.Cache.Extensions
{
    /// <summary>
    /// Service collection extension methods for registering SOPs caching services and providers.
    /// </summary>
    public static class SopsCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Registers cache infrastructure and resolves the active <see cref="ICacheService"/> based on application configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root configuration.</param>
        /// <param name="sectionName">The configuration section name. Defaults to "Cache".</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsCache(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "Cache")
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            IConfigurationSection section = configuration.GetSection(sectionName);
            var settings = new CacheSettings();
            section.Bind(settings);

            // Fail fast at startup if configuration is invalid
            ConfigurationValidator.Validate(settings);

            services.Configure<CacheSettings>(section);
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers cache infrastructure with custom settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The configured cache settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsCache(
            this IServiceCollection services,
            CacheSettings settings)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers cache infrastructure using a configuration delegate.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsCache(
            this IServiceCollection services,
            Action<CacheSettings> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var settings = new CacheSettings();
            configure(settings);
            ConfigurationValidator.Validate(settings);

            services.AddSingleton(Options.Create(settings));
            return RegisterCoreServices(services);
        }

        /// <summary>
        /// Registers a custom cache provider factory in the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="factory">The factory delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCacheProvider(
            this IServiceCollection services,
            string providerName,
            Func<IServiceProvider?, ICacheService> factory)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            services.Configure<CacheProviderRegistryOptions>(options =>
            {
                options.CustomRegistrations[providerName] = factory;
            });

            return services;
        }

        private static IServiceCollection RegisterCoreServices(IServiceCollection services)
        {
            services.TryAddSingleton<ICacheProviderRegistry>(sp =>
            {
                var registry = new CacheProviderRegistry();

                // Register built-in providers
                registry.Register("Memory", serviceProvider =>
                {
                    var cacheSettings = serviceProvider?.GetService<IOptions<CacheSettings>>()?.Value;
                    var memSettings = serviceProvider?.GetService<IOptions<Sops.Cache.Memory.Configuration.MemoryCacheSettings>>()?.Value;
                    var logger = serviceProvider?.GetService<Microsoft.Extensions.Logging.ILogger<Sops.Cache.Memory.MemoryCacheService>>();
                    var memCache = serviceProvider?.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                    return new Sops.Cache.Memory.MemoryCacheService(cacheSettings, memSettings, logger, memCache);
                });

                registry.Register("Redis", serviceProvider =>
                {
                    var cacheSettings = serviceProvider?.GetService<IOptions<CacheSettings>>()?.Value;
                    var rSettings = serviceProvider?.GetService<IOptions<Sops.Cache.Redis.Configuration.RedisCacheSettings>>()?.Value;
                    var logger = serviceProvider?.GetService<Microsoft.Extensions.Logging.ILogger<Sops.Cache.Redis.RedisCacheService>>();
                    var mux = serviceProvider?.GetService<StackExchange.Redis.IConnectionMultiplexer>();

                    if (mux != null)
                    {
                        return new Sops.Cache.Redis.RedisCacheService(mux, cacheSettings, rSettings, logger);
                    }

                    if (rSettings != null)
                    {
                        return new Sops.Cache.Redis.RedisCacheService(rSettings, cacheSettings, logger);
                    }

                    throw new InvalidOperationException("RedisCacheSettings or IConnectionMultiplexer must be registered in the service provider to use the Redis cache provider.");
                });

                var options = sp.GetService<IOptions<CacheProviderRegistryOptions>>();
                if (options?.Value != null)
                {
                    foreach (var pair in options.Value.CustomRegistrations)
                    {
                        registry.Register(pair.Key, pair.Value);
                    }
                }
                return registry;
            });

            services.TryAddTransient<ICacheService>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<CacheSettings>>().Value;
                var registry = sp.GetRequiredService<ICacheProviderRegistry>();
                return registry.CreateProvider(settings.Provider, sp);
            });

            return services;
        }
    }

    /// <summary>
    /// Internal options container for custom cache provider registrations.
    /// </summary>
    internal sealed class CacheProviderRegistryOptions
    {
        public Dictionary<string, Func<IServiceProvider?, ICacheService>> CustomRegistrations { get; } =
            new Dictionary<string, Func<IServiceProvider?, ICacheService>>(StringComparer.OrdinalIgnoreCase);
    }
}
