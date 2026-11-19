using System;

namespace Sops.Cache.Abstractions
{
    /// <summary>
    /// Provides options for expiration and eviction policies of an individual cache entry.
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Gets or sets an absolute expiration time, relative to now.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        /// <summary>
        /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed.
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Creates a <see cref="CacheEntryOptions"/> instance configured with an absolute expiration in seconds.
        /// </summary>
        /// <param name="seconds">Number of seconds until expiration.</param>
        /// <returns>A new <see cref="CacheEntryOptions"/> instance.</returns>
        public static CacheEntryOptions FromSeconds(int seconds) =>
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds) };

        /// <summary>
        /// Creates a <see cref="CacheEntryOptions"/> instance configured with an absolute expiration in minutes.
        /// </summary>
        /// <param name="minutes">Number of minutes until expiration.</param>
        /// <returns>A new <see cref="CacheEntryOptions"/> instance.</returns>
        public static CacheEntryOptions FromMinutes(int minutes) =>
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes) };

        /// <summary>
        /// Creates a <see cref="CacheEntryOptions"/> instance configured with an absolute expiration in hours.
        /// </summary>
        /// <param name="hours">Number of hours until expiration.</param>
        /// <returns>A new <see cref="CacheEntryOptions"/> instance.</returns>
        public static CacheEntryOptions FromHours(int hours) =>
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(hours) };
    }
}
