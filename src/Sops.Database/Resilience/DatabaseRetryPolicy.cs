using System;
using Sops.Database.Configuration;

namespace Sops.Database.Resilience
{
    /// <summary>
    /// Encapsulates retry rules, bounded attempts, backoff timing, and transient exception detection for database operations.
    /// </summary>
    public sealed class DatabaseRetryPolicy
    {
        /// <summary>
        /// Gets the maximum number of attempts (including the initial execution). Bounded between 1 and 10.
        /// </summary>
        public int MaxAttempts { get; }

        /// <summary>
        /// Gets the base initial delay before the first retry attempt.
        /// </summary>
        public TimeSpan InitialDelay { get; }

        /// <summary>
        /// Gets the maximum delay ceiling between retry attempts.
        /// </summary>
        public TimeSpan MaxDelay { get; }

        /// <summary>
        /// Gets the exponential backoff multiplier applied across attempts.
        /// </summary>
        public double BackoffMultiplier { get; }

        /// <summary>
        /// Gets the predicate delegate used to determine whether an exception represents a transient failure.
        /// </summary>
        public Func<Exception, bool> TransientDetector { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseRetryPolicy"/> class.
        /// </summary>
        /// <param name="maxAttempts">Maximum attempts (between 1 and 10).</param>
        /// <param name="initialDelay">Initial delay before retrying.</param>
        /// <param name="maxDelay">Upper delay limit.</param>
        /// <param name="backoffMultiplier">Multiplier factor.</param>
        /// <param name="transientDetector">Custom transient failure detector.</param>
        public DatabaseRetryPolicy(
            int maxAttempts = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0,
            Func<Exception, bool>? transientDetector = null)
        {
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "MaxAttempts must be at least 1.");
            if (maxAttempts > 10) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "MaxAttempts cannot exceed 10 to prevent unbounded retries.");

            MaxAttempts = maxAttempts;
            InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(200);
            MaxDelay = maxDelay ?? TimeSpan.FromMilliseconds(2000);
            BackoffMultiplier = backoffMultiplier >= 1.0 ? backoffMultiplier : 2.0;
            TransientDetector = transientDetector ?? DefaultTransientDetector;
        }

        /// <summary>
        /// Creates a <see cref="DatabaseRetryPolicy"/> configured from a <see cref="DatabaseRetrySettings"/> instance.
        /// </summary>
        /// <param name="settings">The configured retry settings.</param>
        /// <param name="customDetector">Optional custom transient exception detector.</param>
        /// <returns>A new <see cref="DatabaseRetryPolicy"/> instance.</returns>
        public static DatabaseRetryPolicy FromSettings(
            DatabaseRetrySettings settings,
            Func<Exception, bool>? customDetector = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (!settings.Enabled)
            {
                return None;
            }

            return new DatabaseRetryPolicy(
                maxAttempts: settings.MaxAttempts,
                initialDelay: settings.GetInitialDelay(),
                maxDelay: settings.GetMaxDelay(),
                backoffMultiplier: settings.BackoffMultiplier,
                transientDetector: customDetector);
        }

        /// <summary>
        /// Gets a policy that disables retries (attempts once with no retries).
        /// </summary>
        public static DatabaseRetryPolicy None { get; } = new DatabaseRetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero, 1.0, _ => false);

        /// <summary>
        /// Default detector determining whether an exception is likely transient (e.g., timeout, connection reset).
        /// </summary>
        /// <param name="exception">The exception to evaluate.</param>
        /// <returns><c>true</c> if transient; otherwise, <c>false</c>.</returns>
        public static bool DefaultTransientDetector(Exception exception)
        {
            if (exception == null) return false;

            if (exception is TimeoutException)
            {
                return true;
            }

            string message = exception.Message ?? string.Empty;
            if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("deadlock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("connection reset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("temporarily unavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("database is locked", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (exception.InnerException != null)
            {
                return DefaultTransientDetector(exception.InnerException);
            }

            return false;
        }
    }
}
