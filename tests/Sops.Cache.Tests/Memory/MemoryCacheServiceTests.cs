using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Extensions;
using Sops.Cache.Memory;
using Sops.Cache.Memory.Configuration;
using Sops.Cache.Memory.Extensions;
using Xunit;

namespace Sops.Cache.Tests.Memory
{
    public class MemoryCacheServiceTests
    {
        private record TestProfile(string Name, int Age);

        [Fact]
        public async Task HitAndMiss_WorkAsExpected()
        {
            using var cache = new MemoryCacheService();

            CacheResult<string> miss = await cache.GetAsync<string>("missing-key");
            Assert.False(miss.IsHit);
            Assert.Null(miss.Value);

            await cache.SetAsync("existing-key", "val-123");

            CacheResult<string> hit = await cache.GetAsync<string>("existing-key");
            Assert.True(hit.IsHit);
            Assert.Equal("val-123", hit.Value);
        }

        [Fact]
        public async Task TypedValues_StoreAndRetrieveCorrectly()
        {
            using var cache = new MemoryCacheService();

            var profile = new TestProfile("John Doe", 35);
            await cache.SetAsync("user:profile", profile);

            CacheResult<TestProfile> result = await cache.GetAsync<TestProfile>("user:profile");
            Assert.True(result.IsHit);
            Assert.NotNull(result.Value);
            Assert.Equal("John Doe", result.Value.Name);
            Assert.Equal(35, result.Value.Age);
        }

        [Fact]
        public async Task StoredNull_DistinguishesFromCacheMiss()
        {
            using var cache = new MemoryCacheService();

            await cache.SetAsync<string?>("null-entry", null);

            CacheResult<string?> result = await cache.GetAsync<string?>("null-entry");
            Assert.True(result.IsHit);
            Assert.Null(result.Value);

            CacheResult<string?> missing = await cache.GetAsync<string?>("really-missing");
            Assert.False(missing.IsHit);
        }

        [Fact]
        public async Task TtlExpiration_EvictsEntryAfterTtl()
        {
            using var cache = new MemoryCacheService();

            await cache.SetAsync(
                "temp-key",
                "temp-value",
                new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50) });

            CacheResult<string> immediate = await cache.GetAsync<string>("temp-key");
            Assert.True(immediate.IsHit);

            await Task.Delay(100);

            CacheResult<string> expired = await cache.GetAsync<string>("temp-key");
            Assert.False(expired.IsHit);
        }

        [Fact]
        public async Task KeyNamespace_PrefixesKeysProperly()
        {
            var cacheSettings = new CacheSettings
            {
                KeyPrefix = "ns"
            };

            using var cache = new MemoryCacheService(cacheSettings);

            await cache.SetAsync("myKey", "myVal");

            // Key exists with prefix
            Assert.True(await cache.ExistsAsync("myKey"));
            Assert.True((await cache.GetAsync<string>("myKey")).IsHit);
        }

        [Fact]
        public async Task RemoveAndExists_WorkCorrectly()
        {
            using var cache = new MemoryCacheService();

            Assert.False(await cache.ExistsAsync("del-key"));
            Assert.False(await cache.RemoveAsync("del-key"));

            await cache.SetAsync("del-key", "del-val");
            Assert.True(await cache.ExistsAsync("del-key"));

            Assert.True(await cache.RemoveAsync("del-key"));
            Assert.False(await cache.ExistsAsync("del-key"));
        }

        [Fact]
        public async Task ConcurrentAccess_HandlesParallelOperationsCleanly()
        {
            using var cache = new MemoryCacheService();
            int taskCount = 50;
            var tasks = new List<Task>();

            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    string key = $"concurrent:key:{index}";
                    await cache.SetAsync(key, index);
                    CacheResult<int> res = await cache.GetAsync<int>(key);
                    Assert.True(res.IsHit);
                    Assert.Equal(index, res.Value);
                    await cache.RemoveAsync(key);
                }));
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task DisposedService_ThrowsObjectDisposedException()
        {
            var cache = new MemoryCacheService();
            cache.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetAsync<string>("k"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.SetAsync("k", "v"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.RemoveAsync("k"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.ExistsAsync("k"));
        }

        [Fact]
        public void DependencyInjection_RegistersAndResolvesMemoryCacheService()
        {
            var services = new ServiceCollection();
            services.AddSopsCache(options =>
            {
                options.Provider = "Memory";
                options.KeyPrefix = "app";
            });
            services.AddSopsMemoryCache(options =>
            {
                options.SizeLimit = 1000;
            });

            using var provider = services.BuildServiceProvider();

            var cache = provider.GetRequiredService<ICacheService>();
            Assert.NotNull(cache);
            Assert.IsType<MemoryCacheService>(cache);
        }
    }
}
