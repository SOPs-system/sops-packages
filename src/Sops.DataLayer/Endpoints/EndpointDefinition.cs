using System;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Metadata describing an endpoint contract, backend destination, and operational policies.
    /// </summary>
    public sealed class EndpointDefinition
    {
        public string Id { get; }
        public BackendTarget Target { get; }
        public TransportMode Transport { get; }
        public string HttpMethod { get; }
        public string Route { get; }
        public string? RpcService { get; }
        public string? RpcMethod { get; }
        public ApiVersion Version { get; }
        public bool RequiresAuthentication { get; }
        public string? RequiredPermission { get; }
        public bool IsIdempotent { get; }
        public TimeSpan Timeout { get; }
        public EndpointRetryPolicy RetryPolicy { get; }
        public EndpointCachePolicy CachePolicy { get; }
        public EndpointOfflinePolicy OfflinePolicy { get; }
        public SensitivityClassification Sensitivity { get; }
        public bool IsFinancialMutation { get; }
        public bool RequiresIdempotencyKey { get; }
        public long MaxPayloadBytes { get; }

        public EndpointDefinition(
            string id,
            BackendTarget target,
            TransportMode transport,
            string httpMethod,
            string route,
            string? rpcService = null,
            string? rpcMethod = null,
            ApiVersion? version = null,
            bool requiresAuthentication = true,
            string? requiredPermission = null,
            bool isIdempotent = false,
            TimeSpan? timeout = null,
            EndpointRetryPolicy? retryPolicy = null,
            EndpointCachePolicy? cachePolicy = null,
            EndpointOfflinePolicy offlinePolicy = EndpointOfflinePolicy.OnlineOnly,
            SensitivityClassification sensitivity = SensitivityClassification.Normal,
            bool isFinancialMutation = false,
            bool requiresIdempotencyKey = false,
            long maxPayloadBytes = 67108864)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Endpoint ID cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(route)) throw new ArgumentException("Route cannot be empty.", nameof(route));
            if (string.IsNullOrWhiteSpace(httpMethod)) throw new ArgumentException("HTTP method cannot be empty.", nameof(httpMethod));

            Id = id.Trim().ToLowerInvariant();
            Target = target;
            Transport = transport;
            HttpMethod = httpMethod.Trim().ToUpperInvariant();
            Route = route.Trim();
            RpcService = rpcService;
            RpcMethod = rpcMethod;
            Version = version ?? ApiVersion.V1;
            RequiresAuthentication = requiresAuthentication;
            RequiredPermission = requiredPermission;
            IsIdempotent = isIdempotent;
            Timeout = timeout ?? TimeSpan.FromSeconds(30);
            RetryPolicy = retryPolicy ?? (isIdempotent ? EndpointRetryPolicy.Standard : EndpointRetryPolicy.None);
            CachePolicy = cachePolicy ?? EndpointCachePolicy.NoCache;
            OfflinePolicy = offlinePolicy;
            Sensitivity = sensitivity;
            IsFinancialMutation = isFinancialMutation || sensitivity == SensitivityClassification.Financial;
            RequiresIdempotencyKey = requiresIdempotencyKey || IsFinancialMutation;
            MaxPayloadBytes = maxPayloadBytes > 0 ? maxPayloadBytes : 67108864;
        }

        public override string ToString() => $"[{Id}] {HttpMethod} {Route} -> {Target} ({Transport})";
    }
}
