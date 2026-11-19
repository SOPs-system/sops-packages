using System;
using System.Collections.Generic;
using System.Threading;

namespace Sops.DataLayer.Models.Requests
{
    /// <summary>
    /// Per-request execution overrides and cancellation options.
    /// </summary>
    public sealed class RequestOptions
    {
        public static readonly RequestOptions Default = new RequestOptions();

        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public bool ForceRefresh { get; set; }
        public bool CacheOnly { get; set; }
        public string? IdempotencyKey { get; set; }
        public string? CorrelationId { get; set; }
        public IDictionary<string, string>? AdditionalHeaders { get; set; }
        public IDictionary<string, string>? QueryParameters { get; set; }
    }
}
