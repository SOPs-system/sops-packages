using System;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Transport mechanism used for the endpoint.
    /// </summary>
    public enum TransportMode
    {
        /// <summary>Local server RPC route (/rpc/{service}/{method}).</summary>
        Rpc = 0,

        /// <summary>Standard HTTP REST route.</summary>
        HttpRest = 1,

        /// <summary>Server-Sent Events streaming (/api/live).</summary>
        ServerSentEvents = 2
    }

    /// <summary>
    /// Sensitivity classification of data handled by the endpoint for redaction and telemetry.
    /// </summary>
    public enum SensitivityClassification
    {
        Normal = 0,
        InternalOnly = 1,
        Operational = 1,
        PersonallyIdentifiable = 2,
        Financial = 3,
        SecuritySensitive = 4
    }

    /// <summary>
    /// Offline handling policy for an endpoint.
    /// </summary>
    public enum EndpointOfflinePolicy
    {
        /// <summary>Requires online connection; fails immediately when offline.</summary>
        OnlineOnly = 0,
        ServerOnly = 0,

        /// <summary>Can read from cache when offline.</summary>
        CacheFallback = 1,

        /// <summary>Can be queued into an authorized durable outbox when offline.</summary>
        QueueOfflineAllowed = 2
    }

    /// <summary>
    /// Caching strategy for an endpoint.
    /// </summary>
    public sealed class EndpointCachePolicy
    {
        public static readonly EndpointCachePolicy NoCache = new EndpointCachePolicy(false, TimeSpan.Zero);

        public bool IsCacheable { get; }
        public TimeSpan Duration { get; }
        public bool IsSessionScoped { get; }

        public EndpointCachePolicy(bool isCacheable, TimeSpan duration, bool isSessionScoped = false)
        {
            IsCacheable = isCacheable;
            Duration = duration;
            IsSessionScoped = isSessionScoped;
        }

        public static EndpointCachePolicy Minutes(int minutes, bool isSessionScoped = false) =>
            new EndpointCachePolicy(true, TimeSpan.FromMinutes(minutes), isSessionScoped);
    }

    /// <summary>
    /// Retry strategy for an endpoint.
    /// </summary>
    public sealed class EndpointRetryPolicy
    {
        public static readonly EndpointRetryPolicy None = new EndpointRetryPolicy(0, false);
        public static readonly EndpointRetryPolicy Standard = new EndpointRetryPolicy(3, true);

        public int MaxRetries { get; }
        public bool AllowRetry { get; }

        public EndpointRetryPolicy(int maxRetries, bool allowRetry)
        {
            MaxRetries = Math.Max(0, maxRetries);
            AllowRetry = allowRetry && maxRetries > 0;
        }
    }
}
