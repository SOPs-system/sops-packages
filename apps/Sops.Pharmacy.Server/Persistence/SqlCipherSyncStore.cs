using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.DataOperations.Sync.Storage;
using Sops.Models.Sync;

namespace Sops.Pharmacy.Server.Persistence
{
    public class SqlCipherOutboxStore : IOutboxStore
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;

        public SqlCipherOutboxStore(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<long> EnqueueAsync(SyncEventEnvelope envelope, CancellationToken ct = default)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                var id = envelope.EventId == Guid.Empty ? Guid.NewGuid().ToString() : envelope.EventId.ToString();
                var now = DateTime.UtcNow.ToString("o");

                cmd.CommandText = @"
                    INSERT INTO outbox_events (id, aggregate_type, aggregate_id, event_type, payload_json, created_at, status)
                    VALUES (@id, @aggType, @aggId, @evtType, @payload, @now, 'Pending');
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@aggType", envelope.AggregateType ?? "Unknown");
                cmd.Parameters.AddWithValue("@aggId", envelope.AggregateId ?? id);
                cmd.Parameters.AddWithValue("@evtType", envelope.EventType ?? "Unknown");
                cmd.Parameters.AddWithValue("@payload", envelope.PayloadJson ?? "{}");
                cmd.Parameters.AddWithValue("@now", now);

                var rowId = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                return rowId;
            }
        }

        public async Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int maxCount, CancellationToken ct = default)
        {
            var list = new List<OutboxEntry>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT rowid, id, aggregate_type, aggregate_id, event_type, payload_json, created_at, retry_count
                    FROM outbox_events
                    WHERE status = 'Pending'
                    ORDER BY rowid ASC
                    LIMIT @limit;";
                cmd.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(maxCount, 500)));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var rowId = reader.GetInt64(0);
                        var eventIdStr = reader.GetString(1);
                        Guid.TryParse(eventIdStr, out var eventId);

                        var envelope = new SyncEventEnvelope
                        {
                            EventId = eventId,
                            AggregateType = reader.GetString(2),
                            AggregateId = reader.GetString(3),
                            EventType = reader.GetString(4),
                            PayloadJson = reader.GetString(5),
                            OccurredAtUtc = DateTimeOffset.Parse(reader.GetString(6))
                        };

                        list.Add(new OutboxEntry
                        {
                            SequenceNumber = rowId,
                            Envelope = envelope,
                            IsDispatched = false,
                            IsAcknowledged = false,
                            EnqueuedAtUtc = envelope.OccurredAtUtc,
                            DeliveryAttempts = reader.GetInt32(7)
                        });
                    }
                }
            }
            return list;
        }

        public async Task CommitAcknowledgmentAsync(long acknowledgedSequence, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE outbox_events 
                    SET status = 'Dispatched', dispatched_at = @now
                    WHERE rowid <= @seq AND status = 'Pending';";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@seq", acknowledgedSequence);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task RecordFailureAsync(long sequenceNumber, string error, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE outbox_events 
                    SET retry_count = retry_count + 1
                    WHERE rowid = @seq;";
                cmd.Parameters.AddWithValue("@seq", sequenceNumber);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM outbox_events WHERE status = 'Pending';";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                return count;
            }
        }

        public async Task<long> GetLatestSequenceAsync(CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(MAX(rowid), 0) FROM outbox_events;";
                var seq = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                return seq;
            }
        }
    }

    public class SqlCipherInboxStore : IInboxStore
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;

        public SqlCipherInboxStore(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM inbox_events WHERE aggregate_id = @id AND status = 'Processed';";
                cmd.Parameters.AddWithValue("@id", eventId.ToString());
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                return count > 0;
            }
        }

        public async Task EnqueueAsync(SyncEventEnvelope envelope, CancellationToken ct = default)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                var id = "inbox_" + Guid.NewGuid().ToString("N");
                var now = DateTime.UtcNow.ToString("o");

                cmd.CommandText = @"
                    INSERT INTO inbox_events (id, aggregate_type, aggregate_id, event_type, payload_json, received_at, status)
                    VALUES (@id, @aggType, @aggId, @evtType, @payload, @now, 'Pending');";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@aggType", envelope.AggregateType ?? "Unknown");
                cmd.Parameters.AddWithValue("@aggId", envelope.EventId.ToString());
                cmd.Parameters.AddWithValue("@evtType", envelope.EventType ?? "Unknown");
                cmd.Parameters.AddWithValue("@payload", envelope.PayloadJson ?? "{}");
                cmd.Parameters.AddWithValue("@now", now);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<InboxEntry>> GetPendingAsync(int maxCount, CancellationToken ct = default)
        {
            var list = new List<InboxEntry>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, aggregate_type, aggregate_id, event_type, payload_json, received_at
                    FROM inbox_events
                    WHERE status = 'Pending'
                    ORDER BY rowid ASC
                    LIMIT @limit;";
                cmd.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(maxCount, 500)));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var eventIdStr = reader.GetString(2);
                        Guid.TryParse(eventIdStr, out var eventId);

                        var envelope = new SyncEventEnvelope
                        {
                            EventId = eventId,
                            AggregateType = reader.GetString(1),
                            AggregateId = eventIdStr,
                            EventType = reader.GetString(3),
                            PayloadJson = reader.GetString(4),
                            OccurredAtUtc = DateTimeOffset.Parse(reader.GetString(5))
                        };

                        list.Add(new InboxEntry
                        {
                            EventId = eventId,
                            Envelope = envelope,
                            IsProcessed = false,
                            ReceivedAtUtc = envelope.OccurredAtUtc
                        });
                    }
                }
            }
            return list;
        }

        public async Task MarkProcessedAsync(Guid eventId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE inbox_events 
                    SET status = 'Processed', processed_at = @now
                    WHERE aggregate_id = @id;";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", eventId.ToString());

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task MarkFailedAsync(Guid eventId, string error, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE inbox_events 
                    SET status = 'Failed'
                    WHERE aggregate_id = @id;";
                cmd.Parameters.AddWithValue("@id", eventId.ToString());

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM inbox_events WHERE status = 'Pending';";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                return count;
            }
        }
    }
}
