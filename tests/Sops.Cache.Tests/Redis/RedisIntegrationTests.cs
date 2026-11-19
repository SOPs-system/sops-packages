using System;
using System.Threading.Tasks;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Redis;
using Sops.Cache.Redis.Configuration;
using StackExchange.Redis;
using Xunit;

namespace Sops.Cache.Tests.Redis
{
    public class RedisIntegrationTests
    {
        private static async Task<IConnectionMultiplexer?> TryConnectLocalRedisAsync()
        {
            try
            {
                var options = new ConfigurationOptions
                {
                    EndPoints = { "127.0.0.1:6379" },
                    ConnectTimeout = 500,
                    SyncTimeout = 500,
                    AbortOnConnectFail = true
                };

                return await ConnectionMultiplexer.ConnectAsync(options);
            }
            catch
            {
                return null;
            }
        }

        [Fact]
        public async Task LiveRedis_IntegrationOperations_ExecuteWhenRedisIsAvailable()
        {
            using IConnectionMultiplexer? mux = await TryConnectLocalRedisAsync();

            if (mux == null || !mux.IsConnected)
            {
                // Local Redis server is not running on 127.0.0.1:6379.
                // Run unit tests with mocks and skip live Redis when server is not running.
                return;
            }

            var cacheSettings = new CacheSettings
            {
                KeyPrefix = "test-live",
                DefaultTtlSeconds = 60
            };

            var redisSettings = new RedisCacheSettings
            {
                Database = 15 // Use isolated DB for testing
            };

            using var cache = new RedisCacheService(mux, cacheSettings, redisSettings);

            string testKey = "live-key-" + Guid.NewGuid().ToString("N");
            string testVal = "live-val-123";

            // Set
            await cache.SetAsync(testKey, testVal, CacheEntryOptions.FromSeconds(30));

            // Exists
            Assert.True(await cache.ExistsAsync(testKey));

            // Get
            CacheResult<string> res = await cache.GetAsync<string>(testKey);
            Assert.True(res.IsHit);
            Assert.Equal(testVal, res.Value);

            // Health
            Assert.True(await cache.CheckHealthAsync());

            // Remove
            Assert.True(await cache.RemoveAsync(testKey));
            Assert.False(await cache.ExistsAsync(testKey));
        }
    }
}
