using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.Cache.Abstractions;

namespace Sops.Cache.Extensions
{
    /// <summary>
    /// Provides cache-aside pattern extension methods for <see cref="ICacheService"/>.
    /// </summary>
    public static class CacheAsideExtensions
    {
        /// <summary>
        /// Retrieves an item from the cache if present; otherwise, executes the factory, caches the returned value, and returns it.
        /// </summary>
        /// <remarks>
        /// <b>Concurrency note:</b> This helper performs local cache-aside retrieval. In distributed or concurrent scenarios
        /// without distributed locking, multiple simultaneous requests on a cache miss may execute the factory concurrently;
        /// the last completed execution will populate the cache.
        /// Exceptions thrown by <paramref name="factory"/> are never cached and propagate immediately to the caller.
        /// </remarks>
        /// <typeparam name="T">The type of the item being cached.</typeparam>
        /// <param name="cache">The cache service.</param>
        /// <param name="key">The cache key.</param>
        /// <param name="factory">An asynchronous factory delegate that generates the value on a cache miss.</param>
        /// <param name="options">Optional cache entry expiration options.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The cached or freshly generated value.</returns>
        public static async Task<T> GetOrCreateAsync<T>(
            this ICacheService cache,
            string key,
            Func<CancellationToken, Task<T>> factory,
            CacheEntryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            CacheResult<T> cached = await cache.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (cached.IsHit)
            {
                return cached.Value!;
            }

            // Execute the factory on cache miss. If the factory throws an exception, it propagates untouched and is not cached.
            T created = await factory(cancellationToken).ConfigureAwait(false);

            await cache.SetAsync(key, created, options, cancellationToken).ConfigureAwait(false);

            return created;
        }
    }
}
