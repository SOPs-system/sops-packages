using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Sync.Storage;
using Sops.Models.Sync;

namespace Sops.DataOperations.Approvals
{
    /// <summary>
    /// Dispatches created and decided change requests to the durable outbox and cloud services.
    /// </summary>
    public sealed class ChangeRequestDispatcher
    {
        private readonly IOutboxStore _outbox;
        private readonly ISyncTransport _transport;

        public ChangeRequestDispatcher(IOutboxStore outbox, ISyncTransport transport)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task DispatchCreatedRequestAsync(ChangeRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Wrap in SyncEventEnvelope and enqueue into durable outbox
            var envelope = new SyncEventEnvelope
            {
                EventId = Guid.NewGuid(),
                AggregateType = "ChangeRequest",
                AggregateId = request.RequestId.ToString(),
                EventType = "ChangeRequestSubmitted",
                MutationKind = MutationKind.Created,
                OccurredAtUtc = request.CreatedAtUtc,
                TenantId = request.TenantId,
                BranchId = request.BranchId,
                PayloadJson = JsonSerializer.Serialize(request)
            };

            await _outbox.EnqueueAsync(envelope, ct);

            // Attempt direct cloud dispatch if connected
            bool connected = await _transport.CheckGlobalConnectivityAsync(ct);
            if (connected)
            {
                await _transport.DispatchChangeRequestAsync(request, ct);
            }
        }

        public async Task DispatchDecisionAsync(ChangeRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var envelope = new SyncEventEnvelope
            {
                EventId = Guid.NewGuid(),
                AggregateType = "ChangeRequest",
                AggregateId = request.RequestId.ToString(),
                EventType = request.Status == ChangeRequestStatus.Approved ? "ChangeRequestApproved" : "ChangeRequestRejected",
                MutationKind = MutationKind.ActionExecuted,
                OccurredAtUtc = request.Decision?.DecidedAtUtc ?? DateTimeOffset.UtcNow,
                TenantId = request.TenantId,
                BranchId = request.BranchId,
                PayloadJson = JsonSerializer.Serialize(request)
            };

            await _outbox.EnqueueAsync(envelope, ct);
        }
    }
}
