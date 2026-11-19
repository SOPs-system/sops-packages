using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.Cache.Abstractions;
using Sops.Cache.Registration;
using Xunit;

namespace Sops.Cache.Tests
{
    public class CacheProviderRegistryTests
    {
        private class DummyCacheService : ICacheService
        {
            public Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult(CacheResult<T>.Miss());

            public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);

            public void Dispose() { }
        }

        [Fact]
        public void Registry_RegistersAndResolvesCustomProvider()
        {
            var registry = new CacheProviderRegistry();
            registry.Register("Dummy", sp => new DummyCacheService());

            Assert.True(registry.IsRegistered("Dummy"));
            Assert.True(registry.IsRegistered("dummy"));

            ICacheService service = registry.CreateProvider("Dummy");
            Assert.NotNull(service);
            Assert.IsType<DummyCacheService>(service);
        }

        [Fact]
        public void Registry_UnregisteredProvider_ThrowsNotSupportedException()
        {
            var registry = new CacheProviderRegistry();

            var ex = Assert.Throws<NotSupportedException>(() => registry.CreateProvider("Memcached"));

            Assert.Contains("Memcached", ex.Message);
            Assert.Contains("is not registered", ex.Message);
        }

        [Fact]
        public void Registry_ArgumentValidation()
        {
            var registry = new CacheProviderRegistry();

            Assert.Throws<ArgumentException>(() => registry.Register("", sp => new DummyCacheService()));
            Assert.Throws<ArgumentNullException>(() => registry.Register("Valid", null!));
            Assert.Throws<ArgumentException>(() => registry.CreateProvider(""));
        }
    }
}
