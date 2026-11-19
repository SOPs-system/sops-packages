using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Cache.Abstractions;
using Sops.Cache.Extensions;
using Xunit;

namespace Sops.Cache.Tests
{
    public class CacheAsideTests
    {
        private class FakeCacheService : ICacheService
        {
            public Dictionary<string, object?> Store { get; } = new Dictionary<string, object?>();

            public Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Store.TryGetValue(key, out object? val))
                {
                    return Task.FromResult(CacheResult<T>.Hit((T)val!));
                }
                return Task.FromResult(CacheResult<T>.Miss());
            }

            public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Store[key] = value;
                return Task.CompletedTask;
            }

            public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Store.Remove(key));
            }

            public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Store.ContainsKey(key));
            }

            public void Dispose() { }
        }

        [Fact]
        public async Task GetOrCreateAsync_OnCacheHit_ReturnsCachedValue_WithoutInvokingFactory()
        {
            var cache = new FakeCacheService();
            await cache.SetAsync("item:1", "cached-data");

            int factoryInvocations = 0;

            string value = await cache.GetOrCreateAsync(
                "item:1",
                async ct =>
                {
                    factoryInvocations++;
                    await Task.Yield();
                    return "fresh-data";
                });

            Assert.Equal("cached-data", value);
            Assert.Equal(0, factoryInvocations);
        }

        [Fact]
        public async Task GetOrCreateAsync_OnCacheMiss_InvokesFactoryAndCachesResult()
        {
            var cache = new FakeCacheService();
            int factoryInvocations = 0;

            string value = await cache.GetOrCreateAsync(
                "item:2",
                async ct =>
                {
                    factoryInvocations++;
                    await Task.Yield();
                    return "computed-result";
                });

            Assert.Equal("computed-result", value);
            Assert.Equal(1, factoryInvocations);

            // Subsequent call should hit cache
            string cachedValue = await cache.GetOrCreateAsync<string>(
                "item:2",
                ct => throw new InvalidOperationException("Factory should not be invoked"));

            Assert.Equal("computed-result", cachedValue);
        }

        [Fact]
        public async Task GetOrCreateAsync_WhenFactoryThrows_PropagatesExceptionAndDoesNotCache()
        {
            var cache = new FakeCacheService();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetOrCreateAsync<string>(
                    "failed-item",
                    ct => throw new InvalidOperationException("External service failed.")));

            Assert.Equal("External service failed.", ex.Message);
            Assert.False(await cache.ExistsAsync("failed-item"));
        }

        [Fact]
        public async Task GetOrCreateAsync_ObservesCancellationToken()
        {
            var cache = new FakeCacheService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                cache.GetOrCreateAsync(
                    "item:cancelled",
                    ct => Task.FromResult("val"),
                    cancellationToken: cts.Token));
        }

        [Fact]
        public async Task GetOrCreateAsync_ArgumentValidation()
        {
            var cache = new FakeCacheService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                cache.GetOrCreateAsync("", ct => Task.FromResult("val")));

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                cache.GetOrCreateAsync<string>("key", null!));
        }
    }
}
