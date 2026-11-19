using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Redis.Configuration;
using StackExchange.Redis;

namespace Sops.Cache.Redis
{
    /// <summary>
    /// Production-quality Redis distributed cache provider implementing <see cref="ICacheService"/> via <see cref="IConnectionMultiplexer"/>.
    /// Reuses a single multiplexer connection, enforces safe typed JSON serialization, and protects credentials.
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IConnectionMultiplexer? _multiplexer;
        private readonly Lazy<IConnectionMultiplexer> _lazyMultiplexer;
        private readonly bool _ownsMultiplexer;
        private readonly int _databaseIndex;
        private readonly string? _keyPrefix;
        private readonly TimeSpan _defaultTtl;
        private readonly ILogger? _logger;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisCacheService"/> class with existing connection multiplexer.
        /// </summary>
        /// <param name="multiplexer">An established <see cref="IConnectionMultiplexer"/> instance.</param>
        /// <param name="cacheSettings">Global cache settings containing TTL and optional prefix.</param>
        /// <param name="redisSettings">Redis specific configuration.</param>
        /// <param name="logger">Optional diagnostic logger.</param>
        public RedisCacheService(
            IConnectionMultiplexer multiplexer,
            CacheSettings? cacheSettings = null,
            RedisCacheSettings? redisSettings = null,
            ILogger<RedisCacheService>? logger = null)
        {
            _multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
            _ownsMultiplexer = false;
            _lazyMultiplexer = new Lazy<IConnectionMultiplexer>(() => _multiplexer);
            _databaseIndex = redisSettings?.Database ?? 0;
            _keyPrefix = cacheSettings?.KeyPrefix;
            _defaultTtl = cacheSettings != null ? cacheSettings.GetDefaultTtl() : TimeSpan.FromSeconds(300);
            _logger = logger;

            _logger?.LogInformation("Initialized RedisCacheService with injected connection (Database={Db}, KeyPrefix='{Prefix}').",
                _databaseIndex,
                _keyPrefix ?? "[None]");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisCacheService"/> class creating a connection lazily from settings.
        /// </summary>
        /// <param name="redisSettings">Redis specific configuration including connection string.</param>
        /// <param name="cacheSettings">Global cache settings containing TTL and optional prefix.</param>
        /// <param name="logger">Optional diagnostic logger.</param>
        public RedisCacheService(
            RedisCacheSettings redisSettings,
            CacheSettings? cacheSettings = null,
            ILogger<RedisCacheService>? logger = null)
        {
            if (redisSettings == null) throw new ArgumentNullException(nameof(redisSettings));

            _ownsMultiplexer = true;
            _databaseIndex = redisSettings.Database;
            _keyPrefix = cacheSettings?.KeyPrefix;
            _defaultTtl = cacheSettings != null ? cacheSettings.GetDefaultTtl() : TimeSpan.FromSeconds(300);
            _logger = logger;

            _lazyMultiplexer = new Lazy<IConnectionMultiplexer>(() =>
            {
                try
                {
                    var config = ConfigurationOptions.Parse(redisSettings.ConnectionString);
                    config.ConnectTimeout = redisSettings.ConnectTimeoutMilliseconds;
                    config.SyncTimeout = redisSettings.SyncTimeoutMilliseconds;
                    config.AbortOnConnectFail = redisSettings.AbortOnConnectFail;

                    _logger?.LogInformation("Connecting to Redis (Database={Db}, Timeout={Timeout}ms)...",
                        _databaseIndex,
                        redisSettings.ConnectTimeoutMilliseconds);

                    return ConnectionMultiplexer.Connect(config);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        "Failed to initialize Redis connection for database {Db}. Error: {ErrorType}: {ErrorMessage}",
                        _databaseIndex,
                        ex.GetType().Name,
                        ex.Message);
                    throw;
                }
            });
        }

        private IDatabase GetDatabase()
        {
            ThrowIfDisposed();
            IConnectionMultiplexer mux = _multiplexer ?? _lazyMultiplexer.Value;
            return mux.GetDatabase(_databaseIndex);
        }

        /// <inheritdoc />
        public async Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalizedKey = NormalizeKey(key);

            try
            {
                IDatabase db = GetDatabase();
                RedisValue value = await db.StringGetAsync(normalizedKey).ConfigureAwait(false);

                if (value.IsNullOrEmpty)
                {
                    return CacheResult<T>.Miss();
                }

                T? deserialized = JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
                return CacheResult<T>.Hit(deserialized);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger?.LogError(
                    "Redis error during GetAsync for key '{Key}'. Error: {ErrorType}: {ErrorMessage}",
                    key,
                    ex.GetType().Name,
                    ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalizedKey = NormalizeKey(key);

            TimeSpan? expiry = options?.AbsoluteExpirationRelativeToNow ??
                               options?.SlidingExpiration ??
                               _defaultTtl;

            try
            {
                IDatabase db = GetDatabase();
                string json = JsonSerializer.Serialize(value, JsonOptions);
                await db.StringSetAsync(normalizedKey, json, expiry).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger?.LogError(
                    "Redis error during SetAsync for key '{Key}'. Error: {ErrorType}: {ErrorMessage}",
                    key,
                    ex.GetType().Name,
                    ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalizedKey = NormalizeKey(key);

            try
            {
                IDatabase db = GetDatabase();
                return await db.KeyDeleteAsync(normalizedKey).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger?.LogError(
                    "Redis error during RemoveAsync for key '{Key}'. Error: {ErrorType}: {ErrorMessage}",
                    key,
                    ex.GetType().Name,
                    ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalizedKey = NormalizeKey(key);

            try
            {
                IDatabase db = GetDatabase();
                return await db.KeyExistsAsync(normalizedKey).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger?.LogError(
                    "Redis error during ExistsAsync for key '{Key}'. Error: {ErrorType}: {ErrorMessage}",
                    key,
                    ex.GetType().Name,
                    ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Executes an active health probe against the Redis database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if responsive; otherwise, <c>false</c>.</returns>
        public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IDatabase db = GetDatabase();
                TimeSpan latency = await db.PingAsync().ConfigureAwait(false);
                return latency >= TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Redis health probe failed: {ErrorType}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                return false;
            }
        }

        private string NormalizeKey(string key) =>
            string.IsNullOrEmpty(_keyPrefix) ? key : $"{_keyPrefix}:{key}";

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RedisCacheService), "The Redis cache service has already been disposed.");
            }
        }

        /// <summary>
        /// Disposes the underlying Redis connection multiplexer if owned by this instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing && _ownsMultiplexer && _lazyMultiplexer.IsValueCreated)
                {
                    try
                    {
                        _lazyMultiplexer.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("Error disposing Redis multiplexer: {ErrorMessage}", ex.Message);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
