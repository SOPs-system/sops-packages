using System;

namespace Sops.DataLayer.Configuration
{
    /// <summary>
    /// Configuration options for the SOPs Client DataLayer.
    /// Follows Sops.Configuration conventions.
    /// </summary>
    public class DataLayerOptions
    {
        /// <summary>Default port used by local pharmacy server.</summary>
        public const int DefaultLocalServerPort = 5000;

        /// <summary>Alternative legacy port used by local pharmacy server.</summary>
        public const int LegacyLocalServerPort = 8714;

        /// <summary>Base URL for the local pharmacy server (LAN or loopback).</summary>
        public Uri LocalServerAddress { get; set; } = new Uri($"http://127.0.0.1:{DefaultLocalServerPort}");

        /// <summary>Base URL for the global cloud backend (HTTPS only in production).</summary>
        public Uri GlobalApiAddress { get; set; } = new Uri("https://api.sops.pharmacy");

        /// <summary>Default request timeout in seconds.</summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>Timeout for connection handshake in seconds.</summary>
        public int ConnectionTimeoutSeconds { get; set; } = 5;

        /// <summary>Maximum number of retries for idempotent operations.</summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>Base retry delay in milliseconds.</summary>
        public int RetryBaseDelayMs { get; set; } = 300;

        /// <summary>Maximum retry backoff delay in milliseconds.</summary>
        public int RetryMaxDelayMs { get; set; } = 5000;

        /// <summary>Whether live feed (SSE / long-polling) is enabled.</summary>
        public bool EnableLiveFeed { get; set; } = true;

        /// <summary>Polling interval in milliseconds when falling back to long-polling.</summary>
        public int LivePollingIntervalMs { get; set; } = 1000;

        /// <summary>Interval for background health checks in seconds.</summary>
        public int HealthCheckIntervalSeconds { get; set; } = 15;

        /// <summary>Whether in-process memory caching is enabled for read operations.</summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>Default cache time-to-live in minutes.</summary>
        public int DefaultCacheTtlMinutes { get; set; } = 15;

        /// <summary>Terminal ID identifying this POS station.</summary>
        public string TerminalId { get; set; } = Environment.MachineName;

        /// <summary>Optional Pharmacy ID.</summary>
        public string? PharmacyId { get; set; }

        /// <summary>Optional Branch ID.</summary>
        public string? BranchId { get; set; }

        /// <summary>Optional Tenant ID for multi-tenant deployments.</summary>
        public string? TenantId { get; set; }

        /// <summary>Client software version string.</summary>
        public string ClientVersion { get; set; } = "1.0.0";

        /// <summary>Maximum request payload size in bytes (default: 64 MB).</summary>
        public long MaxRequestBodyBytes { get; set; } = 67108864;

        /// <summary>Approved hosts allowlist for SSRF prevention. Requests targeting unlisted hosts are rejected.</summary>
        public System.Collections.Generic.HashSet<string> AllowedHosts { get; set; } = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "127.0.0.1",
            "localhost",
            "::1",
            "api.sops.pharmacy"
        };

        /// <summary>Validates the options.</summary>
        public void Validate()
        {
            if (LocalServerAddress == null)
            {
                throw new ArgumentNullException(nameof(LocalServerAddress), "LocalServerAddress must not be null.");
            }

            if (!string.IsNullOrEmpty(LocalServerAddress.UserInfo))
            {
                throw new InvalidOperationException("LocalServerAddress must not contain user credentials (UserInfo).");
            }

            if (GlobalApiAddress == null)
            {
                throw new ArgumentNullException(nameof(GlobalApiAddress), "GlobalApiAddress must not be null.");
            }

            if (!string.IsNullOrEmpty(GlobalApiAddress.UserInfo))
            {
                throw new InvalidOperationException("GlobalApiAddress must not contain user credentials (UserInfo).");
            }

            if (RequestTimeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(RequestTimeoutSeconds), "RequestTimeoutSeconds must be greater than zero.");
            }

            if (MaxRetryAttempts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxRetryAttempts), "MaxRetryAttempts must be non-negative.");
            }
        }
    }
}
