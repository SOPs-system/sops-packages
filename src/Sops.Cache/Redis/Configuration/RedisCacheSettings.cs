using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Models;

namespace Sops.Cache.Redis.Configuration
{
    /// <summary>
    /// Configuration options for the Redis distributed cache provider.
    /// </summary>
    public class RedisCacheSettings : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the Redis connection string (e.g. "localhost:6379,password=secret").
        /// Marked as sensitive to prevent leakage in logs and exception messages.
        /// </summary>
        [Sensitive]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target Redis logical database index. Defaults to 0.
        /// </summary>
        [Range(0, 10000)]
        public int Database { get; set; } = 0;

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds. Defaults to 5000 ms.
        /// </summary>
        [Range(100, 60000)]
        public int ConnectTimeoutMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the timeout in milliseconds for synchronous Redis operations. Defaults to 5000 ms.
        /// </summary>
        [Range(100, 60000)]
        public int SyncTimeoutMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Gets or sets a value indicating whether connection initialization should abort on immediate connect failure.
        /// Defaults to <c>false</c> to permit lazy connection retry.
        /// </summary>
        public bool AbortOnConnectFail { get; set; } = false;

        /// <summary>
        /// Validates Redis configuration options.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>A collection of validation errors.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                yield return new ValidationResult(
                    "Redis ConnectionString cannot be null or empty.",
                    new[] { nameof(ConnectionString) });
            }
        }

        /// <summary>
        /// Returns a diagnostic summary of the Redis configuration with credentials masked.
        /// </summary>
        /// <returns>A sanitized diagnostic string.</returns>
        public string ToSafeSummary()
        {
            string masked = string.IsNullOrWhiteSpace(ConnectionString) ? "[Not Configured]" : "[REDACTED]";
            return $"RedisDatabase={Database}, ConnectTimeout={ConnectTimeoutMilliseconds}ms, SyncTimeout={SyncTimeoutMilliseconds}ms, Connection={masked}";
        }
    }
}
