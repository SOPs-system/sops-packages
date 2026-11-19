using System;

namespace Sops.DataLayer.Models.Responses
{
    /// <summary>
    /// Source origin from which data was retrieved.
    /// </summary>
    public enum DataSourceOrigin
    {
        LocalServer = 0,
        GlobalApi = 1,
        MemoryCache = 2,
        OfflineOutbox = 3,
        Fallback = 4
    }

    /// <summary>
    /// Metadata capturing provenance, freshness, caching state, and execution metrics of a response.
    /// </summary>
    public sealed class ResponseMetadata
    {
        public DataSourceOrigin Source { get; }
        public bool IsFresh { get; }
        public bool IsStale { get; }
        public DateTimeOffset? CachedAtUtc { get; }
        public DateTimeOffset? ServerTimestampUtc { get; }
        public TimeSpan Duration { get; }
        public int? StatusCode { get; }
        public string? CorrelationId { get; }
        public string? ApiVersion { get; }
        public string? ServerVersionOrETag { get; }
        public TimeSpan? RetryAfter { get; }
        public int RetryCount { get; }

        public ResponseMetadata(
            DataSourceOrigin source,
            bool isFresh = true,
            bool isStale = false,
            DateTimeOffset? cachedAtUtc = null,
            DateTimeOffset? serverTimestampUtc = null,
            TimeSpan duration = default,
            int? statusCode = null,
            string? correlationId = null,
            string? apiVersion = null,
            string? serverVersionOrETag = null,
            TimeSpan? retryAfter = null,
            int retryCount = 0)
        {
            Source = source;
            IsFresh = isFresh;
            IsStale = isStale;
            CachedAtUtc = cachedAtUtc;
            ServerTimestampUtc = serverTimestampUtc;
            Duration = duration;
            StatusCode = statusCode;
            CorrelationId = correlationId;
            ApiVersion = apiVersion;
            ServerVersionOrETag = serverVersionOrETag;
            RetryAfter = retryAfter;
            RetryCount = retryCount;
        }

        public static ResponseMetadata Live(DataSourceOrigin source, TimeSpan duration, int? statusCode = null, string? correlationId = null, string? serverVersionOrETag = null, TimeSpan? retryAfter = null) =>
            new ResponseMetadata(source, isFresh: true, isStale: false, duration: duration, statusCode: statusCode, correlationId: correlationId, serverVersionOrETag: serverVersionOrETag, retryAfter: retryAfter);

        public static ResponseMetadata Cached(DateTimeOffset cachedAtUtc, bool isStale = false, string? correlationId = null) =>
            new ResponseMetadata(DataSourceOrigin.MemoryCache, isFresh: !isStale, isStale: isStale, cachedAtUtc: cachedAtUtc, correlationId: correlationId);

        public static ResponseMetadata Stale(DateTimeOffset cachedAtUtc, string? correlationId = null) =>
            new ResponseMetadata(DataSourceOrigin.MemoryCache, isFresh: false, isStale: true, cachedAtUtc: cachedAtUtc, correlationId: correlationId);
    }
}
