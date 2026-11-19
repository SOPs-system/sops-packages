using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Extensions;
using Sops.Cache.Redis.Configuration;
using Sops.Cache.Registration;
using StackExchange.Redis;

namespace Sops.Cache.Redis.Extensions
{
    /// <summary>
    /// Service collection extension methods for registering the Redis distributed cache provider.
    /// </summary>
    public static class SopsRedisCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Redis distributed cache provider with the SOPs caching system.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional delegate to configure Redis settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsRedisCache(
            this IServiceCollection services,
            Action<RedisCacheSettings>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var redisSettings = new RedisCacheSettings();
            configure?.Invoke(redisSettings);
            services.AddSingleton(Options.Create(redisSettings));

            services.AddCacheProvider("Redis", sp =>
            {
                var cacheSettings = sp?.GetService<IOptions<CacheSettings>>()?.Value;
                var rSettings = sp?.GetService<IOptions<RedisCacheSettings>>()?.Value ?? redisSettings;
                var logger = sp?.GetService<ILogger<RedisCacheService>>();
                var mux = sp?.GetService<IConnectionMultiplexer>();

                if (mux != null)
                {
                    return new RedisCacheService(mux, cacheSettings, rSettings, logger);
                }

                return new RedisCacheService(rSettings, cacheSettings, logger);
            });

            return services;
        }
    }
}
