using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Extensions;
using Sops.Cache.Memory.Configuration;
using Sops.Cache.Registration;

namespace Sops.Cache.Memory.Extensions
{
    /// <summary>
    /// Service collection extension methods for registering the in-memory cache provider.
    /// </summary>
    public static class SopsMemoryCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the in-memory cache provider with the SOPs caching system.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional delegate to configure in-memory cache settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsMemoryCache(
            this IServiceCollection services,
            Action<MemoryCacheSettings>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var memorySettings = new MemoryCacheSettings();
            configure?.Invoke(memorySettings);
            services.AddSingleton(Options.Create(memorySettings));

            services.AddCacheProvider("Memory", sp =>
            {
                var cacheSettings = sp?.GetService<IOptions<CacheSettings>>()?.Value;
                var memSettings = sp?.GetService<IOptions<MemoryCacheSettings>>()?.Value;
                var logger = sp?.GetService<ILogger<MemoryCacheService>>();
                var memCache = sp?.GetService<IMemoryCache>();
                return new MemoryCacheService(cacheSettings, memSettings, logger, memCache);
            });

            return services;
        }
    }
}
