using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sops.Models.Sync;

namespace Sops.DataOperations.Sync
{
    /// <summary>
    /// Information about a detected mutation conflict during synchronization.
    /// </summary>
    public sealed class ConflictRecord
    {
        public Guid ConflictId { get; set; } = Guid.NewGuid();
        public Guid EventId { get; set; }
        public string AggregateType { get; set; } = string.Empty;
        public string AggregateId { get; set; } = string.Empty;
        public long LocalRevision { get; set; }
        public long IncomingRevision { get; set; }
        public string LocalPayloadJson { get; set; } = string.Empty;
        public string IncomingPayloadJson { get; set; } = string.Empty;
        public DateTimeOffset DetectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public bool IsResolved { get; set; }
        public string? ResolutionNotes { get; set; }
    }

    /// <summary>
    /// Evaluates concurrent revision drift, protects against overwriting newer local or cloud state,
    /// and quarantines unresolved conflicts for manager review.
    /// </summary>
    public sealed class SyncConflictHandler
    {
        private readonly ConcurrentDictionary<Guid, ConflictRecord> _quarantinedConflicts = new ConcurrentDictionary<Guid, ConflictRecord>();

        public IReadOnlyList<ConflictRecord> GetQuarantinedConflicts()
        {
            return _quarantinedConflicts.Values.Where(c => !c.IsResolved).ToList();
        }

        public Task<bool> DetectAndHandleConflictAsync(
            SyncEventEnvelope incomingEvent,
            long currentLocalRevision,
            long incomingBaseRevision,
            CancellationToken ct = default)
        {
            if (incomingEvent == null) throw new ArgumentNullException(nameof(incomingEvent));

            // If base revision matches, no conflict
            if (incomingBaseRevision == currentLocalRevision)
            {
                return Task.FromResult(false);
            }

            // Conflict detected: base revision mismatch
            var conflict = new ConflictRecord
            {
                EventId = incomingEvent.EventId,
                AggregateType = incomingEvent.AggregateType,
                AggregateId = incomingEvent.AggregateId,
                LocalRevision = currentLocalRevision,
                IncomingRevision = incomingBaseRevision,
                IncomingPayloadJson = incomingEvent.PayloadJson,
                DetectedAtUtc = DateTimeOffset.UtcNow
            };

            _quarantinedConflicts.TryAdd(conflict.ConflictId, conflict);
            return Task.FromResult(true);
        }

        public Task ResolveConflictAsync(Guid conflictId, string resolutionNotes, CancellationToken ct = default)
        {
            if (_quarantinedConflicts.TryGetValue(conflictId, out var conflict))
            {
                conflict.IsResolved = true;
                conflict.ResolutionNotes = resolutionNotes;
            }
            return Task.CompletedTask;
        }
    }
}
