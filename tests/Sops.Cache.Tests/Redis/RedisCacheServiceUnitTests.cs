using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Extensions;
using Sops.Cache.Redis;
using Sops.Cache.Redis.Configuration;
using Sops.Cache.Redis.Extensions;
using StackExchange.Redis;
using Xunit;

namespace Sops.Cache.Tests.Redis
{
    public class RedisCacheServiceUnitTests
    {
        private record TestUser(string Name, int Age);

        [Fact]
        public async Task GetAsync_WhenKeyExists_ReturnsHitWithDeserializedObject()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var db = Substitute.For<IDatabase>();
            mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

            string json = "{\"Name\":\"Alice\",\"Age\":30}";
            db.StringGetAsync((RedisKey)"test:user:1", Arg.Any<CommandFlags>())
              .Returns(Task.FromResult((RedisValue)json));

            var cacheSettings = new CacheSettings { KeyPrefix = "test" };
            var redisSettings = new RedisCacheSettings { Database = 0 };

            using var cache = new RedisCacheService(mux, cacheSettings, redisSettings);

            CacheResult<TestUser> result = await cache.GetAsync<TestUser>("user:1");

            Assert.True(result.IsHit);
            Assert.NotNull(result.Value);
            Assert.Equal("Alice", result.Value.Name);
            Assert.Equal(30, result.Value.Age);
        }

        [Fact]
        public async Task GetAsync_WhenKeyMissing_ReturnsMiss()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var db = Substitute.For<IDatabase>();
            mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

            db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
              .Returns(Task.FromResult(RedisValue.Null));

            using var cache = new RedisCacheService(mux);

            CacheResult<TestUser> result = await cache.GetAsync<TestUser>("user:missing");

            Assert.False(result.IsHit);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task SetAsync_SerializesJsonAndPassesExpiry()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var db = Substitute.For<IDatabase>();
            mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

            db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
              .Returns(Task.FromResult(true));

            var cacheSettings = new CacheSettings { KeyPrefix = "sops", DefaultTtlSeconds = 600 };
            using var cache = new RedisCacheService(mux, cacheSettings);

            var user = new TestUser("Bob", 25);
            await cache.SetAsync("user:2", user, CacheEntryOptions.FromMinutes(5));

            await db.Received(1).StringSetAsync(
                (RedisKey)"sops:user:2",
                Arg.Is<RedisValue>(v => ((string)v!).Contains("Bob")),
                TimeSpan.FromMinutes(5),
                Arg.Any<bool>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>());
        }

        [Fact]
        public async Task RemoveAsync_CallsKeyDelete()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var db = Substitute.For<IDatabase>();
            mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

            db.KeyDeleteAsync((RedisKey)"del-key", Arg.Any<CommandFlags>())
              .Returns(Task.FromResult(true));

            using var cache = new RedisCacheService(mux);

            bool removed = await cache.RemoveAsync("del-key");
            Assert.True(removed);
        }

        [Fact]
        public async Task ExistsAsync_CallsKeyExists()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var db = Substitute.For<IDatabase>();
            mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

            db.KeyExistsAsync((RedisKey)"exists-key", Arg.Any<CommandFlags>())
              .Returns(Task.FromResult(true));

            using var cache = new RedisCacheService(mux);

            bool exists = await cache.ExistsAsync("exists-key");
            Assert.True(exists);
        }

        [Fact]
        public async Task CheckHealthAsync_ReturnsTrueWhenPingSucceeds()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var db = Substitute.For<IDatabase>();
            mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

            db.PingAsync(Arg.Any<CommandFlags>())
              .Returns(Task.FromResult(TimeSpan.FromMilliseconds(2)));

            using var cache = new RedisCacheService(mux);

            bool healthy = await cache.CheckHealthAsync();
            Assert.True(healthy);
        }

        [Fact]
        public async Task DisposedService_ThrowsObjectDisposedException()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();
            var cache = new RedisCacheService(mux);
            cache.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetAsync<string>("k"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.SetAsync("k", "v"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.RemoveAsync("k"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.ExistsAsync("k"));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.CheckHealthAsync());
        }

        [Fact]
        public void DependencyInjection_RegistersAndResolvesRedisCacheService()
        {
            var mux = Substitute.For<IConnectionMultiplexer>();

            var services = new ServiceCollection();
            services.AddSingleton(mux);

            services.AddSopsCache(options =>
            {
                options.Provider = "Redis";
                options.KeyPrefix = "sops";
            });

            services.AddSopsRedisCache(options =>
            {
                options.ConnectionString = "localhost:6379";
                options.Database = 1;
            });

            using var provider = services.BuildServiceProvider();

            var cache = provider.GetRequiredService<ICacheService>();
            Assert.NotNull(cache);
            Assert.IsType<RedisCacheService>(cache);
        }
    }
}
