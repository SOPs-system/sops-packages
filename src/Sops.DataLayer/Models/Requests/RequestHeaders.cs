namespace Sops.DataLayer.Models.Requests
{
    /// <summary>
    /// Standard HTTP and RPC headers used by the SOPs client.
    /// </summary>
    public static class RequestHeaders
    {
        public const string CorrelationId = "X-Correlation-ID";
        public const string CausationId = "X-Causation-ID";
        public const string RequestId = "X-Request-ID";
        public const string IdempotencyKey = "X-Idempotency-Key";
        public const string ClientVersion = "X-Client-Version";
        public const string TerminalId = "X-Terminal-ID";
        public const string PharmacyId = "X-Pharmacy-ID";
        public const string BranchId = "X-Branch-ID";
        public const string TenantId = "X-Tenant-ID";
        public const string ExpectedVersion = "If-Match";
        public const string Authorization = "Authorization";
    }
}
