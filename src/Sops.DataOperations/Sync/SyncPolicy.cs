using System;

namespace Sops.DataOperations.Sync
{
    /// <summary>
    /// Configuration policy governing synchronization intervals, batch limits, and retry backoff.
    /// </summary>
    public sealed class SyncPolicy
    {
        public int DefaultBatchSize { get; set; } = 100;
        public int MaxBatchSize { get; set; } = 500;
        public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxRetryAttempts { get; set; } = 5;
        public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(2);
        public double JitterFactor { get; set; } = 0.2;
        public bool AutoSyncEnabled { get; set; } = true;
        public TimeSpan AutoSyncInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Calculates exponential backoff with jitter for a given retry attempt.
        /// </summary>
        public TimeSpan CalculateBackoff(int attempt)
        {
            if (attempt <= 0) return TimeSpan.Zero;
            double factor = Math.Pow(2, Math.Min(attempt, 6));
            double baseDelayMs = InitialRetryDelay.TotalMilliseconds * factor;
            baseDelayMs = Math.Min(baseDelayMs, MaxRetryDelay.TotalMilliseconds);

            // Jitter: +/- JitterFactor
            var rng = new Random();
            double jitter = (rng.NextDouble() * 2.0 - 1.0) * JitterFactor;
            double finalDelayMs = Math.Max(100, baseDelayMs * (1.0 + jitter));

            return TimeSpan.FromMilliseconds(finalDelayMs);
        }
    }
}
