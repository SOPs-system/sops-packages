using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Cache.Abstractions;
using Sops.Cache.Configuration;
using Sops.Cache.Memory.Configuration;

namespace Sops.Cache.Memory
{
    /// <summary>
    /// Thread-safe in-memory cache provider implementing <see cref="ICacheService"/> via <see cref="IMemoryCache"/>.
    /// </summary>
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly bool _ownsCache;
        private readonly string? _keyPrefix;
        private readonly TimeSpan _defaultTtl;
        private readonly long? _sizeLimit;
        private readonly ILogger? _logger;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheService"/> class.
        /// </summary>
        /// <param name="cacheSettings">Global cache settings containing TTL and optional prefix.</param>
        /// <param name="memorySettings">Specific memory cache options (size limits, compaction).</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="memoryCache">Optional external <see cref="IMemoryCache"/> instance.</param>
        public MemoryCacheService(
            CacheSettings? cacheSettings = null,
            MemoryCacheSettings? memorySettings = null,
            ILogger<MemoryCacheService>? logger = null,
            IMemoryCache? memoryCache = null)
        {
            _logger = logger;
            _keyPrefix = cacheSettings?.KeyPrefix;
            _defaultTtl = cacheSettings != null ? cacheSettings.GetDefaultTtl() : TimeSpan.FromSeconds(300);
            _sizeLimit = memorySettings?.SizeLimit;

            if (memoryCache != null)
            {
                _memoryCache = memoryCache;
                _ownsCache = false;
            }
            else
            {
                var options = new MemoryCacheOptions
                {
                    CompactionPercentage = memorySettings?.CompactionPercentage ?? 0.05,
                    ExpirationScanFrequency = TimeSpan.FromSeconds(memorySettings?.ExpirationScanFrequencySeconds ?? 60),
                    SizeLimit = memorySettings?.SizeLimit
                };

                _memoryCache = new MemoryCache(options);
                _ownsCache = true;
            }

            _logger?.LogInformation("Initialized MemoryCacheService (KeyPrefix='{Prefix}', DefaultTtl={DefaultTtl}s).",
                _keyPrefix ?? "[None]",
                _defaultTtl.TotalSeconds);
        }

        /// <inheritdoc />
        public Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalized = NormalizeKey(key);

            if (_memoryCache.TryGetValue(normalized, out object? raw))
            {
                if (raw is T typed)
                {
                    return Task.FromResult(CacheResult<T>.Hit(typed));
                }

                if (raw == null)
                {
                    return Task.FromResult(CacheResult<T>.Hit(default));
                }

                try
                {
                    T converted = (T)Convert.ChangeType(raw, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
                    return Task.FromResult(CacheResult<T>.Hit(converted));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Failed to convert cached entry for key '{Key}' to type '{Type}'. Error: {Message}",
                        key, typeof(T).Name, ex.Message);
                    return Task.FromResult(CacheResult<T>.Miss());
                }
            }

            return Task.FromResult(CacheResult<T>.Miss());
        }

        /// <inheritdoc />
        public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalized = NormalizeKey(key);

            var entryOptions = new MemoryCacheEntryOptions();

            if (options?.AbsoluteExpirationRelativeToNow.HasValue == true)
            {
                entryOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow.Value;
            }
            else if (options?.SlidingExpiration.HasValue == true)
            {
                entryOptions.SlidingExpiration = options.SlidingExpiration.Value;
            }
            else
            {
                entryOptions.AbsoluteExpirationRelativeToNow = _defaultTtl;
            }

            if (_sizeLimit.HasValue)
            {
                entryOptions.Size = 1;
            }

            _memoryCache.Set(normalized, value, entryOptions);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalized = NormalizeKey(key);
            bool existed = _memoryCache.TryGetValue(normalized, out _);

            if (existed)
            {
                _memoryCache.Remove(normalized);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            }

            string normalized = NormalizeKey(key);
            bool exists = _memoryCache.TryGetValue(normalized, out _);
            return Task.FromResult(exists);
        }

        private string NormalizeKey(string key) =>
            string.IsNullOrEmpty(_keyPrefix) ? key : $"{_keyPrefix}:{key}";

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MemoryCacheService), "The cache service has already been disposed.");
            }
        }

        /// <summary>
        /// Releases managed resources used by the in-memory cache service.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing && _ownsCache)
                {
                    _memoryCache.Dispose();
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
