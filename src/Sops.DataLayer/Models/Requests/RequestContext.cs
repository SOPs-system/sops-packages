using System;

namespace Sops.DataLayer.Models.Requests
{
    /// <summary>
    /// Immutable, operation-specific execution context propagated across pipeline steps.
    /// Guarantees that concurrent requests never bleed headers, tokens, or correlation IDs.
    /// </summary>
    public sealed class RequestContext
    {
        public string RequestId { get; }
        public string CorrelationId { get; }
        public string? CausationId { get; }
        public string? EndpointId { get; }
        public string? IdempotencyKey { get; }
        public string TerminalId { get; }
        public string? UserId { get; }
        public string? SessionId { get; }
        public string? PharmacyId { get; }
        public string? BranchId { get; }
        public string? TenantId { get; }
        public string? ExpectedEntityVersion { get; }
        public string ClientVersion { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public DateTimeOffset? DeadlineUtc { get; }

        public RequestContext(
            string? correlationId = null,
            string? causationId = null,
            string? idempotencyKey = null,
            string? terminalId = null,
            string? userId = null,
            string? sessionId = null,
            string? pharmacyId = null,
            string? branchId = null,
            string? tenantId = null,
            string? clientVersion = null,
            DateTimeOffset? deadlineUtc = null,
            string? endpointId = null,
            string? expectedEntityVersion = null)
        {
            RequestId = Guid.NewGuid().ToString("N");
            CorrelationId = !string.IsNullOrWhiteSpace(correlationId) ? correlationId! : Guid.NewGuid().ToString("N");
            CausationId = causationId;
            EndpointId = endpointId;
            IdempotencyKey = idempotencyKey;
            TerminalId = !string.IsNullOrWhiteSpace(terminalId) ? terminalId! : Environment.MachineName;
            UserId = userId;
            SessionId = sessionId;
            PharmacyId = pharmacyId;
            BranchId = branchId;
            TenantId = tenantId;
            ExpectedEntityVersion = expectedEntityVersion;
            ClientVersion = clientVersion ?? "1.0.0";
            CreatedAtUtc = DateTimeOffset.UtcNow;
            DeadlineUtc = deadlineUtc;
        }

        /// <summary>Creates a new context with a specific idempotency key, preserving all other metadata.</summary>
        public RequestContext WithIdempotencyKey(string idempotencyKey)
        {
            return new RequestContext(
                CorrelationId,
                CausationId,
                idempotencyKey,
                TerminalId,
                UserId,
                SessionId,
                PharmacyId,
                BranchId,
                TenantId,
                ClientVersion,
                DeadlineUtc,
                EndpointId,
                ExpectedEntityVersion);
        }

        /// <summary>Creates a new context with overridden properties.</summary>
        public RequestContext WithOverrides(
            string? correlationId = null,
            string? causationId = null,
            string? idempotencyKey = null,
            string? terminalId = null,
            string? userId = null,
            string? sessionId = null,
            string? pharmacyId = null,
            string? branchId = null,
            string? tenantId = null,
            string? clientVersion = null,
            string? endpointId = null,
            string? expectedEntityVersion = null)
        {
            return new RequestContext(
                correlationId ?? CorrelationId,
                causationId ?? CausationId,
                idempotencyKey ?? IdempotencyKey,
                terminalId ?? TerminalId,
                userId ?? UserId,
                sessionId ?? SessionId,
                pharmacyId ?? PharmacyId,
                branchId ?? BranchId,
                tenantId ?? TenantId,
                clientVersion ?? ClientVersion,
                DeadlineUtc,
                endpointId ?? EndpointId,
                expectedEntityVersion ?? ExpectedEntityVersion);
        }
    }
}
