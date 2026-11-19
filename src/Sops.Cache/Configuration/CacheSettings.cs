using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sops.Cache.Configuration
{
    /// <summary>
    /// Configuration settings for the SOPs caching system.
    /// </summary>
    public class CacheSettings : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the caching provider identifier (e.g. "Memory", "Redis").
        /// Defaults to "Memory".
        /// </summary>
        [Required(ErrorMessage = "Cache provider is required.")]
        public string Provider { get; set; } = "Memory";

        /// <summary>
        /// Gets or sets an optional key prefix or namespace prepended to all cache keys (e.g. "sops").
        /// </summary>
        public string? KeyPrefix { get; set; }

        /// <summary>
        /// Gets or sets the default time-to-live (TTL) in seconds applied to cache entries when no explicit expiration is provided.
        /// Defaults to 300 seconds (5 minutes).
        /// </summary>
        [Range(1, 2592000, ErrorMessage = "DefaultTtlSeconds must be between 1 and 2,592,000 seconds (30 days).")]
        public int DefaultTtlSeconds { get; set; } = 300;

        /// <summary>
        /// Gets the configured default TTL as a <see cref="TimeSpan"/>.
        /// </summary>
        /// <returns>The default TTL.</returns>
        public TimeSpan GetDefaultTtl() => TimeSpan.FromSeconds(DefaultTtlSeconds);

        /// <summary>
        /// Validates cross-property consistency rules for cache settings.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>A collection of validation results.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                yield return new ValidationResult(
                    "Cache Provider cannot be null, empty, or whitespace.",
                    new[] { nameof(Provider) });
            }
        }

        /// <summary>
        /// Returns a safe diagnostic summary of cache settings.
        /// </summary>
        /// <returns>A diagnostic string safe for logs.</returns>
        public string ToSafeSummary()
        {
            string prefix = string.IsNullOrWhiteSpace(KeyPrefix) ? "[None]" : KeyPrefix!;
            return $"Provider='{Provider}', KeyPrefix='{prefix}', DefaultTtl={DefaultTtlSeconds}s";
        }
    }
}
