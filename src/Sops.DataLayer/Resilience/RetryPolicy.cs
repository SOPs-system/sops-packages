using System;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Resilience
{
    /// <summary>
    /// Exponential backoff with decorrelated jitter calculation and retry eligibility.
    /// </summary>
    public static class RetryPolicy
    {
        private static readonly Random Rnd = new Random();

        public static bool IsRetryable(EndpointDefinition endpoint, DataOutcome outcome)
        {
            if (endpoint == null) return false;

            // Never retry non-idempotent mutations unless explicitly configured
            if (endpoint.RetryPolicy == EndpointRetryPolicy.None) return false;

            switch (outcome)
            {
                case DataOutcome.ServerUnavailable:
                case DataOutcome.RateLimited:
                case DataOutcome.Timeout:
                    return true;
                default:
                    return false;
            }
        }

        public static TimeSpan ComputeDelay(int attempt, TimeSpan initialDelay, TimeSpan maxDelay)
        {
            if (attempt <= 0) return TimeSpan.Zero;

            // Exponential backoff: 2^(attempt-1) * initialDelay
            double factor = Math.Pow(2, attempt - 1);
            double baseMilliseconds = initialDelay.TotalMilliseconds * factor;

            // Full jitter between 0.5 and 1.5 of base delay
            double jitter;
            lock (Rnd)
            {
                jitter = 0.5 + Rnd.NextDouble();
            }

            double calculatedMs = Math.Min(baseMilliseconds * jitter, maxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(calculatedMs);
        }
    }
}
