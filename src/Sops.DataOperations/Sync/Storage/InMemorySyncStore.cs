using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sops.Models.Sync;

namespace Sops.DataOperations.Sync.Storage
{
    /// <summary>
    /// Thread-safe in-memory implementation of IOutboxStore and IInboxStore for unit testing and local node operations.
    /// </summary>
    public sealed class InMemorySyncStore : IOutboxStore, IInboxStore
    {
        private readonly object _lock = new object();
        private readonly List<OutboxEntry> _outbox = new List<OutboxEntry>();
        private readonly ConcurrentDictionary<Guid, InboxEntry> _inbox = new ConcurrentDictionary<Guid, InboxEntry>();
        private long _currentSequence = 0;

        // --- IOutboxStore Implementation ---

        public Task<long> EnqueueAsync(SyncEventEnvelope envelope, CancellationToken ct = default)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            lock (_lock)
            {
                _currentSequence++;
                var entry = new OutboxEntry
                {
                    SequenceNumber = _currentSequence,
                    Envelope = envelope,
                    IsDispatched = false,
                    IsAcknowledged = false,
                    EnqueuedAtUtc = DateTimeOffset.UtcNow
                };
                _outbox.Add(entry);
                return Task.FromResult(_currentSequence);
            }
        }

        public Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int maxCount, CancellationToken ct = default)
        {
            lock (_lock)
            {
                var pending = _outbox
                    .Where(e => !e.IsAcknowledged)
                    .OrderBy(e => e.SequenceNumber)
                    .Take(maxCount)
                    .ToList();

                return Task.FromResult<IReadOnlyList<OutboxEntry>>(pending);
            }
        }

        public Task CommitAcknowledgmentAsync(long acknowledgedSequence, CancellationToken ct = default)
        {
            lock (_lock)
            {
                foreach (var entry in _outbox.Where(e => e.SequenceNumber <= acknowledgedSequence))
                {
                    entry.IsAcknowledged = true;
                    entry.IsDispatched = true;
                    entry.AcknowledgedAtUtc = DateTimeOffset.UtcNow;
                }
            }
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(long sequenceNumber, string error, CancellationToken ct = default)
        {
            lock (_lock)
            {
                var entry = _outbox.FirstOrDefault(e => e.SequenceNumber == sequenceNumber);
                if (entry != null)
                {
                    entry.DeliveryAttempts++;
                    entry.LastError = error;
                }
            }
            return Task.CompletedTask;
        }

        public Task<int> GetPendingCountAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                int count = _outbox.Count(e => !e.IsAcknowledged);
                return Task.FromResult(count);
            }
        }

        public Task<long> GetLatestSequenceAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_currentSequence);
            }
        }

        // --- IInboxStore Implementation ---

        public Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken ct = default)
        {
            if (_inbox.TryGetValue(eventId, out var entry))
            {
                return Task.FromResult(entry.IsProcessed);
            }
            return Task.FromResult(false);
        }

        Task IInboxStore.EnqueueAsync(SyncEventEnvelope envelope, CancellationToken ct)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            var entry = new InboxEntry
            {
                EventId = envelope.EventId,
                Envelope = envelope,
                IsProcessed = false,
                ReceivedAtUtc = DateTimeOffset.UtcNow
            };

            _inbox.TryAdd(envelope.EventId, entry);
            return Task.CompletedTask;
        }

        Task<IReadOnlyList<InboxEntry>> IInboxStore.GetPendingAsync(int maxCount, CancellationToken ct)
        {
            var pending = _inbox.Values
                .Where(e => !e.IsProcessed)
                .OrderBy(e => e.Envelope.OccurredAtUtc)
                .Take(maxCount)
                .ToList();

            return Task.FromResult<IReadOnlyList<InboxEntry>>(pending);
        }

        public Task MarkProcessedAsync(Guid eventId, CancellationToken ct = default)
        {
            if (_inbox.TryGetValue(eventId, out var entry))
            {
                entry.IsProcessed = true;
                entry.ProcessedAtUtc = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid eventId, string error, CancellationToken ct = default)
        {
            if (_inbox.TryGetValue(eventId, out var entry))
            {
                entry.ErrorMessage = error;
            }
            return Task.CompletedTask;
        }

        Task<int> IInboxStore.GetPendingCountAsync(CancellationToken ct)
        {
            int count = _inbox.Values.Count(e => !e.IsProcessed);
            return Task.FromResult(count);
        }
    }
}
