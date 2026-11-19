using System;
using System.ComponentModel.DataAnnotations;

namespace Sops.Database.Configuration
{
    /// <summary>
    /// Configuration settings for transient database operation retry policies.
    /// </summary>
    public class DatabaseRetrySettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether transient failure retry is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for transient errors.
        /// Must be bounded between 1 and 10 to avoid unbounded execution.
        /// </summary>
        [Range(1, 10)]
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial retry delay in milliseconds before exponential backoff.
        /// </summary>
        [Range(1, 60000)]
        public int InitialDelayMilliseconds { get; set; } = 200;

        /// <summary>
        /// Gets or sets the upper ceiling for retry delays in milliseconds.
        /// </summary>
        [Range(1, 300000)]
        public int MaxDelayMilliseconds { get; set; } = 2000;

        /// <summary>
        /// Gets or sets the multiplier applied to each subsequent retry delay.
        /// </summary>
        [Range(1.0, 10.0)]
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Converts the configured settings into a <see cref="TimeSpan"/> representation of the initial delay.
        /// </summary>
        /// <returns>The initial delay as a <see cref="TimeSpan"/>.</returns>
        public TimeSpan GetInitialDelay() => TimeSpan.FromMilliseconds(InitialDelayMilliseconds);

        /// <summary>
        /// Converts the configured settings into a <see cref="TimeSpan"/> representation of the maximum delay.
        /// </summary>
        /// <returns>The maximum delay as a <see cref="TimeSpan"/>.</returns>
        public TimeSpan GetMaxDelay() => TimeSpan.FromMilliseconds(MaxDelayMilliseconds);
    }
}
