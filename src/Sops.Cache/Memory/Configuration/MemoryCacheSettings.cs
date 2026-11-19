using System;
using System.ComponentModel.DataAnnotations;

namespace Sops.Cache.Memory.Configuration
{
    /// <summary>
    /// Configuration options for the in-memory cache provider.
    /// </summary>
    public class MemoryCacheSettings
    {
        /// <summary>
        /// Gets or sets an optional maximum size limit (in arbitrary entry units) for the in-memory cache.
        /// When exceeded, items will be compacted.
        /// </summary>
        public long? SizeLimit { get; set; }

        /// <summary>
        /// Gets or sets the percentage of the cache to compact when the size limit is exceeded (between 0.01 and 0.50).
        /// Defaults to 0.05 (5%).
        /// </summary>
        [Range(0.01, 0.50)]
        public double CompactionPercentage { get; set; } = 0.05;

        /// <summary>
        /// Gets or sets the scan frequency in seconds for expired items cleanup.
        /// Defaults to 60 seconds.
        /// </summary>
        [Range(1, 3600)]
        public int ExpirationScanFrequencySeconds { get; set; } = 60;
    }
}
