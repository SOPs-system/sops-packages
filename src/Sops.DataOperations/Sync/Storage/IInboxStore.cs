using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Models.Sync;

namespace Sops.DataOperations.Sync.Storage
{
    /// <summary>
    /// Stored inbox event to enforce idempotent deduplication and transactional execution.
    /// </summary>
    public sealed class InboxEntry
    {
        public Guid EventId { get; set; }
        public SyncEventEnvelope Envelope { get; set; } = new SyncEventEnvelope();
        public bool IsProcessed { get; set; }
        public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ProcessedAtUtc { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Durable storage boundary for inbound cloud events ensuring idempotent execution.
    /// </summary>
    public interface IInboxStore
    {
        /// <summary>Checks whether an event has already been received or processed.</summary>
        Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken ct = default);

        /// <summary>Records an incoming cloud event into the inbox.</summary>
        Task EnqueueAsync(SyncEventEnvelope envelope, CancellationToken ct = default);

        /// <summary>Retrieves un-applied inbox events in order.</summary>
        Task<IReadOnlyList<InboxEntry>> GetPendingAsync(int maxCount, CancellationToken ct = default);

        /// <summary>Marks an inbox event as transactionally processed and applied.</summary>
        Task MarkProcessedAsync(Guid eventId, CancellationToken ct = default);

        /// <summary>Marks an inbox event as failed during application.</summary>
        Task MarkFailedAsync(Guid eventId, string error, CancellationToken ct = default);

        /// <summary>Gets the count of pending inbox events awaiting application.</summary>
        Task<int> GetPendingCountAsync(CancellationToken ct = default);
    }
}
