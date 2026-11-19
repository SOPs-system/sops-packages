using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Sync;
using Sops.DataOperations.Sync.Storage;
using Sops.Models.Sync;
using Xunit;

namespace Sops.DataOperations.Tests
{
    public class SyncCoordinatorTests
    {
        [Fact]
        public void SyncPolicy_CalculateBackoff_AppliesExponentialBackoffWithJitter()
        {
            var policy = new SyncPolicy
            {
                InitialRetryDelay = TimeSpan.FromSeconds(1),
                MaxRetryDelay = TimeSpan.FromSeconds(30),
                JitterFactor = 0.2
            };

            // Attempt 0 -> Zero
            var backoff0 = policy.CalculateBackoff(0);
            Assert.Equal(TimeSpan.Zero, backoff0);

            // Attempt 1 -> ~2s (within [1.5, 2.5])
            var backoff1 = policy.CalculateBackoff(1);
            Assert.InRange(backoff1.TotalSeconds, 1.5, 2.5);

            // Attempt 2 -> ~4s (within [3.0, 5.0])
            var backoff2 = policy.CalculateBackoff(2);
            Assert.InRange(backoff2.TotalSeconds, 3.0, 5.0);

            // Attempt 10 -> Capped at MaxRetryDelay (30s) +/- jitter
            var backoff10 = policy.CalculateBackoff(10);
            Assert.InRange(backoff10.TotalSeconds, 23.0, 37.0);
        }

        [Fact]
        public async Task SyncCoordinator_DispatchesOutboxBatch_AndAcksEvents()
        {
            var outbox = new InMemorySyncStore();
            var inbox = new InMemorySyncStore();
            var checkpoints = new SyncCheckpointCoordinator();
            var conflictHandler = new SyncConflictHandler();
            var statusProvider = new SyncStatusProvider(outbox, inbox, checkpoints);
            var transport = Substitute.For<ISyncTransport>();

            // Setup transport
            transport.CheckGlobalConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            transport.CheckTenantConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            transport.UploadBatchAsync(Arg.Any<SyncBatch>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var batch = callInfo.Arg<SyncBatch>();
                    return Task.FromResult(new SyncBatchAck
                    {
                        Success = true,
                        BatchId = batch.BatchId,
                        AcknowledgedEventsCount = batch.Events.Count,
                        CommittedCheckpoint = 100
                    });
                });

            transport.DownloadEventsAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<SyncEventEnvelope>>(new List<SyncEventEnvelope>()));

            var coordinator = new SyncCoordinator(
                outbox,
                inbox,
                transport,
                checkpoints,
                conflictHandler,
                statusProvider,
                new SyncPolicy(),
                NullLogger<SyncCoordinator>.Instance);

            var pharmacyId = Guid.NewGuid();
            var chainId = Guid.NewGuid();

            // Enqueue test outbox events
            var event1 = new SyncEventEnvelope
            {
                EventId = Guid.NewGuid(),
                AggregateType = "DrugInventory",
                AggregateId = "DRUG-001",
                EventType = "StockAdjusted",
                MutationKind = MutationKind.Updated,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                TenantId = chainId,
                BranchId = pharmacyId,
                PayloadJson = "{\"quantity\": 50}"
            };
            var event2 = new SyncEventEnvelope
            {
                EventId = Guid.NewGuid(),
                AggregateType = "SaleTransaction",
                AggregateId = "TXN-999",
                EventType = "SaleCompleted",
                MutationKind = MutationKind.Created,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                TenantId = chainId,
                BranchId = pharmacyId,
                PayloadJson = "{\"total\": 120.50}"
            };

            await coordinator.EnqueueOutboxEventAsync(event1);
            await coordinator.EnqueueOutboxEventAsync(event2);

            // Execute sync pass
            var result = await coordinator.TriggerSyncAsync();

            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.EventsUploaded);
            Assert.Equal(0, result.Data.ConflictsDetected);

            // Verify events are no longer pending
            var pendingAfter = await outbox.GetPendingAsync(10);
            Assert.Empty(pendingAfter);
        }

        [Fact]
        public async Task SyncCoordinator_DownloadsAndProcessesIncomingEvents()
        {
            var outbox = new InMemorySyncStore();
            var inbox = new InMemorySyncStore();
            var checkpoints = new SyncCheckpointCoordinator();
            var conflictHandler = new SyncConflictHandler();
            var statusProvider = new SyncStatusProvider(outbox, inbox, checkpoints);
            var transport = Substitute.For<ISyncTransport>();

            var pharmacyId = Guid.NewGuid();
            var chainId = Guid.NewGuid();
            var newEventId = Guid.NewGuid();

            transport.CheckGlobalConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            transport.CheckTenantConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

            transport.DownloadEventsAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<SyncEventEnvelope>>(new List<SyncEventEnvelope>
                {
                    new SyncEventEnvelope
                    {
                        EventId = newEventId,
                        AggregateType = "PriceList",
                        AggregateId = "PL-02",
                        EventType = "PriceUpdated",
                        MutationKind = MutationKind.Updated,
                        OccurredAtUtc = DateTimeOffset.UtcNow,
                        TenantId = chainId,
                        BranchId = pharmacyId,
                        PayloadJson = "{\"price\": 45.00}"
                    }
                }));

            var coordinator = new SyncCoordinator(
                outbox,
                inbox,
                transport,
                checkpoints,
                conflictHandler,
                statusProvider,
                new SyncPolicy(),
                NullLogger<SyncCoordinator>.Instance);

            var result = await coordinator.TriggerSyncAsync();

            Assert.True(result.Success);
            Assert.Equal(1, result.Data!.EventsDownloaded);

            bool processed = await inbox.HasEventBeenProcessedAsync(newEventId);
            Assert.True(processed);
        }

        [Fact]
        public async Task SyncCoordinator_TransportUnavailable_ReportsOfflineState()
        {
            var outbox = new InMemorySyncStore();
            var inbox = new InMemorySyncStore();
            var checkpoints = new SyncCheckpointCoordinator();
            var conflictHandler = new SyncConflictHandler();
            var statusProvider = new SyncStatusProvider(outbox, inbox, checkpoints);
            var transport = Substitute.For<ISyncTransport>();

            transport.CheckGlobalConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

            var coordinator = new SyncCoordinator(
                outbox,
                inbox,
                transport,
                checkpoints,
                conflictHandler,
                statusProvider,
                new SyncPolicy(),
                NullLogger<SyncCoordinator>.Instance);

            var result = await coordinator.TriggerSyncAsync();

            Assert.False(result.Success);
            Assert.Equal("OFFLINE", result.ErrorCode);

            var status = await coordinator.GetStatusAsync();
            Assert.Equal(SyncState.Offline, status.State);
        }

        [Fact]
        public async Task SyncCoordinator_PauseAndResume_UpdatesStatus()
        {
            var outbox = new InMemorySyncStore();
            var inbox = new InMemorySyncStore();
            var checkpoints = new SyncCheckpointCoordinator();
            var statusProvider = new SyncStatusProvider(outbox, inbox, checkpoints);
            var coordinator = new SyncCoordinator(
                outbox,
                inbox,
                Substitute.For<ISyncTransport>(),
                checkpoints,
                new SyncConflictHandler(),
                statusProvider);

            var initialStatus = await coordinator.GetStatusAsync();
            Assert.NotEqual(SyncState.Paused, initialStatus.State);

            await coordinator.PauseAsync("Maintenance in progress");
            var pausedStatus = await coordinator.GetStatusAsync();
            Assert.Equal(SyncState.Paused, pausedStatus.State);

            await coordinator.ResumeAsync();
            var resumedStatus = await coordinator.GetStatusAsync();
            Assert.NotEqual(SyncState.Paused, resumedStatus.State);
        }

        [Fact]
        public async Task SyncConflictHandler_StaleRevision_QuarantinesEvent()
        {
            var handler = new SyncConflictHandler();
            var incoming = new SyncEventEnvelope
            {
                EventId = Guid.NewGuid(),
                AggregateType = "InventoryItem",
                AggregateId = "SKU-404",
                EventType = "StockChanged",
                MutationKind = MutationKind.Updated,
                OccurredAtUtc = DateTimeOffset.UtcNow,
                PayloadJson = "{\"quantity\": 10}"
            };

            // Local current revision has already moved to 5, incoming was based on revision 3
            long localCurrentRevision = 5;
            long incomingBaseRevision = 3;

            bool isConflict = await handler.DetectAndHandleConflictAsync(incoming, localCurrentRevision, incomingBaseRevision);

            Assert.True(isConflict);
            var quarantined = handler.GetQuarantinedConflicts();
            Assert.Contains(quarantined, q => q.EventId == incoming.EventId);
        }
    }
}
