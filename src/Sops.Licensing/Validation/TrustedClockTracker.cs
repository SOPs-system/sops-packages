using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;

namespace Sops.Licensing.Validation
{
    /// <summary>
    /// Tracks monotonic trusted time in protected secure storage to detect clock rollback attacks
    /// designed to bypass license expiration.
    /// </summary>
    internal class TrustedClockTracker
    {
        private readonly ISecureStorage? _storage;
        private readonly string _clockKey = "trusted.clock.timestamp";
        private readonly int _toleranceSeconds;
        private readonly object _syncLock = new object();

        public TrustedClockTracker(ISecureStorage? storage, int toleranceSeconds = 300)
        {
            _storage = storage;
            _toleranceSeconds = Math.Max(0, toleranceSeconds);
        }

        /// <summary>
        /// Validates current system time against previously recorded trusted timestamps.
        /// Advances the recorded timestamp if the current time is forward.
        /// </summary>
        /// <param name="currentUtc">The current UTC time.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if clock is consistent; <c>false</c> if suspicious backward clock rollback detected.</returns>
        public async Task<bool> ValidateAndAdvanceClockAsync(DateTimeOffset currentUtc, CancellationToken cancellationToken = default)
        {
            if (_storage == null) return true;

            try
            {
                string? storedTimestampStr = await _storage.GetStringAsync(_clockKey, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(storedTimestampStr) &&
                    DateTimeOffset.TryParse(storedTimestampStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var lastTrustedUtc))
                {
                    // If current clock is earlier than last trusted time minus tolerance -> clock rollback!
                    if (currentUtc < lastTrustedUtc.AddSeconds(-_toleranceSeconds))
                    {
                        return false;
                    }
                }

                // If current time is ahead of last trusted time, advance stored timestamp
                string newTimestampStr = currentUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(storedTimestampStr))
                {
                    await _storage.SaveStringAsync(_clockKey, newTimestampStr, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _storage.UpdateStringAsync(_clockKey, newTimestampStr, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            catch
            {
                // In restricted execution environments, fail safe
                return true;
            }
        }
    }
}
