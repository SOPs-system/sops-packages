using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Cache.Abstractions
{
    /// <summary>
    /// Provider-independent caching contract supporting typed retrieval, storage with TTL options, key removal, and existence checks.
    /// </summary>
    public interface ICacheService : IDisposable
    {
        /// <summary>
        /// Asynchronously retrieves an item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of the item to retrieve.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A <see cref="CacheResult{T}"/> indicating hit or miss and the value if present.</returns>
        Task<CacheResult<T>> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously sets an item in the cache with optional expiration options.
        /// </summary>
        /// <typeparam name="T">The type of the item to store.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="options">Optional entry expiration policies.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous set operation.</returns>
        Task SetAsync<T>(
            string key,
            T value,
            CacheEntryOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously removes an item from the cache by its key.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
        Task<bool> RemoveAsync(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously checks whether an item exists in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><c>true</c> if the key exists and has not expired; otherwise, <c>false</c>.</returns>
        Task<bool> ExistsAsync(
            string key,
            CancellationToken cancellationToken = default);
    }
}
