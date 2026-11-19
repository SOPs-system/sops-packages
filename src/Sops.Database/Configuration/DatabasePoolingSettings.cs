using System.ComponentModel.DataAnnotations;

namespace Sops.Database.Configuration
{
    /// <summary>
    /// Configuration settings for database connection pooling.
    /// </summary>
    public class DatabasePoolingSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether connection pooling is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum number of connections maintained in the pool.
        /// </summary>
        [Range(0, 1000)]
        public int MinPoolSize { get; set; } = 0;

        /// <summary>
        /// Gets or sets the maximum number of connections permitted in the pool.
        /// </summary>
        [Range(1, 10000)]
        public int MaxPoolSize { get; set; } = 100;
    }
}
