using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Models.Sync;

namespace Sops.DataOperations.Sync.Storage
{
    /// <summary>
    /// Stored outbox event envelope with sequence tracking and delivery state.
    /// </summary>
    public sealed class OutboxEntry
    {
        public long SequenceNumber { get; set; }
        public SyncEventEnvelope Envelope { get; set; } = new SyncEventEnvelope();
        public bool IsDispatched { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTimeOffset EnqueuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DispatchedAtUtc { get; set; }
        public DateTimeOffset? AcknowledgedAtUtc { get; set; }
        public int DeliveryAttempts { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>
    /// Durable storage boundary for local outbox events.
    /// Guarantees that no outbox record is removed or considered delivered before cloud ACK is received.
    /// </summary>
    public interface IOutboxStore
    {
        /// <summary>Appends a new event envelope to the durable outbox.</summary>
        Task<long> EnqueueAsync(SyncEventEnvelope envelope, CancellationToken ct = default);

        /// <summary>Retrieves up to maxCount unacknowledged events in sequence order.</summary>
        Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int maxCount, CancellationToken ct = default);

        /// <summary>Marks a batch of events as durably acknowledged by the cloud up to acknowledgedSequence.</summary>
        Task CommitAcknowledgmentAsync(long acknowledgedSequence, CancellationToken ct = default);

        /// <summary>Records a failed delivery attempt and error reason.</summary>
        Task RecordFailureAsync(long sequenceNumber, string error, CancellationToken ct = default);

        /// <summary>Gets the count of unacknowledged outbox events.</summary>
        Task<int> GetPendingCountAsync(CancellationToken ct = default);

        /// <summary>Gets the latest assigned sequence number.</summary>
        Task<long> GetLatestSequenceAsync(CancellationToken ct = default);
    }
}
