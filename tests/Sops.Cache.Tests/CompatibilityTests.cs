using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Extensions;
using Sops.Cache.Registration;
using Xunit;

namespace Sops.Cache.Tests
{
    public class CompatibilityTests
    {
        private class TestCacheProvider : ICacheService
        {
            public Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult(CacheResult<T>.Miss());

            public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public void Dispose() { }
        }

        [Fact]
        public void DependencyInjection_ResolvesConfiguredCacheService()
        {
            var configData = new Dictionary<string, string?>
            {
                ["Cache:Provider"] = "CustomTest",
                ["Cache:KeyPrefix"] = "test",
                ["Cache:DefaultTtlSeconds"] = "120"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsCache(configuration);
            services.AddCacheProvider("CustomTest", sp => new TestCacheProvider());

            using var provider = services.BuildServiceProvider();

            var cacheService = provider.GetRequiredService<ICacheService>();
            Assert.NotNull(cacheService);
            Assert.IsType<TestCacheProvider>(cacheService);
        }

        [Fact]
        public void PublicApi_TargetCompatibility_InterfacesAreAccessible()
        {
            Assert.True(typeof(ICacheService).IsInterface);
            Assert.True(typeof(ICacheProviderRegistry).IsInterface);
            Assert.True(typeof(CacheResult<>).IsSealed);
            Assert.True(typeof(CacheEntryOptions).IsClass);
            Assert.True(typeof(CacheSettings).IsClass);

            // Verify consolidated Memory types
            Assert.True(typeof(Sops.Cache.Memory.MemoryCacheService).IsClass);
            Assert.True(typeof(Sops.Cache.Memory.Configuration.MemoryCacheSettings).IsClass);
            Assert.NotNull(typeof(Sops.Cache.Memory.Extensions.SopsMemoryCacheServiceCollectionExtensions));

            // Verify consolidated Redis types
            Assert.True(typeof(Sops.Cache.Redis.RedisCacheService).IsClass);
            Assert.True(typeof(Sops.Cache.Redis.Configuration.RedisCacheSettings).IsClass);
            Assert.NotNull(typeof(Sops.Cache.Redis.Extensions.SopsRedisCacheServiceCollectionExtensions));
        }
    }
}
