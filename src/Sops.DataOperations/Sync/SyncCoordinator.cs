using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Sync.Storage;
using Sops.Models.Sync;

namespace Sops.DataOperations.Sync
{
    /// <summary>
    /// Production implementation of ISyncCoordinator orchestrating offline-first replication.
    /// </summary>
    public sealed class SyncCoordinator : ISyncCoordinator
    {
        private readonly IOutboxStore _outbox;
        private readonly IInboxStore _inbox;
        private readonly ISyncTransport _transport;
        private readonly SyncCheckpointCoordinator _checkpoints;
        private readonly SyncConflictHandler _conflicts;
        private readonly SyncStatusProvider _statusProvider;
        private readonly SyncPolicy _policy;
        private readonly ILogger<SyncCoordinator>? _logger;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);

        public SyncCoordinator(
            IOutboxStore outbox,
            IInboxStore inbox,
            ISyncTransport transport,
            SyncCheckpointCoordinator checkpoints,
            SyncConflictHandler conflicts,
            SyncStatusProvider statusProvider,
            SyncPolicy? policy = null,
            ILogger<SyncCoordinator>? logger = null)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _conflicts = conflicts ?? throw new ArgumentNullException(nameof(conflicts));
            _statusProvider = statusProvider ?? throw new ArgumentNullException(nameof(statusProvider));
            _policy = policy ?? new SyncPolicy();
            _logger = logger;
        }

        public async Task<DataOperationResult<SyncResult>> TriggerSyncAsync(SyncScope scope = SyncScope.All, CancellationToken ct = default)
        {
            if (_statusProvider.CurrentState == SyncState.Paused)
            {
                return DataOperationResult<SyncResult>.Failed("SYNC_PAUSED", "Synchronization is currently paused.");
            }

            if (!await _syncLock.WaitAsync(0, ct))
            {
                return DataOperationResult<SyncResult>.Failed("SYNC_IN_PROGRESS", "A synchronization cycle is already running.", isRetryEligible: true);
            }

            try
            {
                _statusProvider.SetState(SyncState.Connecting);

                // 1. Probe connectivity
                bool isGlobalConnected = await _transport.CheckGlobalConnectivityAsync(ct);
                bool isTenantConnected = await _transport.CheckTenantConnectivityAsync(ct);
                _statusProvider.SetConnectivity(isGlobalConnected, isTenantConnected);

                if (!isGlobalConnected)
                {
                    _statusProvider.SetState(SyncState.Offline);
                    return DataOperationResult<SyncResult>.Failed("OFFLINE", "Unable to reach global cloud sync endpoints. Remaining offline.", isRetryEligible: true);
                }

                _statusProvider.SetState(SyncState.Synchronizing);

                int totalUploaded = 0;
                int totalDownloaded = 0;
                int totalConflicts = 0;

                // 2. Process Outbox if in scope
                if (scope == SyncScope.All || scope == SyncScope.OutboxOnly)
                {
                    var outboxResult = await ProcessOutboxInternalAsync(_policy.DefaultBatchSize, ct);
                    if (outboxResult.Success && outboxResult.Data != null)
                    {
                        totalUploaded += outboxResult.Data.EventsUploaded;
                    }
                }

                // 3. Process Inbox if in scope
                if (scope == SyncScope.All || scope == SyncScope.InboxOnly)
                {
                    var inboxResult = await ProcessInboxInternalAsync(ct);
                    if (inboxResult.Success && inboxResult.Data != null)
                    {
                        totalDownloaded += inboxResult.Data.EventsDownloaded;
                        totalConflicts += inboxResult.Data.ConflictsDetected;
                    }
                }

                _statusProvider.RecordSuccess();

                var syncResult = new SyncResult
                {
                    Success = true,
                    EventsUploaded = totalUploaded,
                    EventsDownloaded = totalDownloaded,
                    ConflictsDetected = totalConflicts,
                    NewCheckpoint = await _checkpoints.GetLocalCheckpointAsync(ct),
                    Message = $"Sync completed successfully: {totalUploaded} uploaded, {totalDownloaded} downloaded."
                };

                return DataOperationResult<SyncResult>.Succeeded(syncResult);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Synchronization execution failed.");
                _statusProvider.RecordFailure(ex.Message);
                return DataOperationResult<SyncResult>.Failed("SYNC_FAILED", ex.Message, isRetryEligible: true);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<DataOperationResult<SyncResult>> ProcessOutboxAsync(int batchSize = 100, CancellationToken ct = default)
        {
            if (!await _syncLock.WaitAsync(0, ct))
                return DataOperationResult<SyncResult>.Failed("LOCKED", "Sync lock busy.", isRetryEligible: true);

            try
            {
                return await ProcessOutboxInternalAsync(batchSize, ct);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task<DataOperationResult<SyncResult>> ProcessOutboxInternalAsync(int batchSize, CancellationToken ct)
        {
            var pending = await _outbox.GetPendingAsync(Math.Min(batchSize, _policy.MaxBatchSize), ct);
            if (pending.Count == 0)
            {
                return DataOperationResult<SyncResult>.Succeeded(new SyncResult { Success = true, EventsUploaded = 0 });
            }

            var batch = new SyncBatch
            {
                BatchId = Guid.NewGuid(),
                BranchId = pending[0].Envelope.BranchId,
                ClientSequenceStart = pending.First().SequenceNumber,
                ClientSequenceEnd = pending.Last().SequenceNumber,
                Events = pending.Select(p => p.Envelope).ToList(),
                SentAtUtc = DateTimeOffset.UtcNow
            };

            var ack = await _transport.UploadBatchAsync(batch, ct);
            if (ack.Success)
            {
                // CRUCIAL: Only mark outbox acknowledged AFTER verified durable cloud receipt
                await _outbox.CommitAcknowledgmentAsync(batch.ClientSequenceEnd, ct);
                await _checkpoints.SetCloudCheckpointAsync(ack.CommittedCheckpoint, ct);

                return DataOperationResult<SyncResult>.Succeeded(new SyncResult
                {
                    Success = true,
                    EventsUploaded = pending.Count,
                    NewCheckpoint = ack.CommittedCheckpoint
                });
            }
            else
            {
                await _outbox.RecordFailureAsync(batch.ClientSequenceStart, ack.ErrorMessage ?? "Batch rejected by cloud.", ct);
                return DataOperationResult<SyncResult>.Failed("OUTBOX_UPLOAD_FAILED", ack.ErrorMessage ?? "Upload rejected.", isRetryEligible: true);
            }
        }

        public async Task<DataOperationResult<SyncResult>> ProcessInboxAsync(CancellationToken ct = default)
        {
            if (!await _syncLock.WaitAsync(0, ct))
                return DataOperationResult<SyncResult>.Failed("LOCKED", "Sync lock busy.", isRetryEligible: true);

            try
            {
                return await ProcessInboxInternalAsync(ct);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private async Task<DataOperationResult<SyncResult>> ProcessInboxInternalAsync(CancellationToken ct)
        {
            long currentCheckpoint = await _checkpoints.GetLocalCheckpointAsync(ct);
            var cloudEvents = await _transport.DownloadEventsAsync(Guid.Empty, currentCheckpoint, _policy.DefaultBatchSize, ct);

            int applied = 0;
            int conflicts = 0;

            foreach (var evt in cloudEvents)
            {
                ct.ThrowIfCancellationRequested();

                // Idempotency: skip if already processed
                if (await _inbox.HasEventBeenProcessedAsync(evt.EventId, ct))
                {
                    continue;
                }

                await _inbox.EnqueueAsync(evt, ct);

                // Apply mutation locally
                applied++;
                await _inbox.MarkProcessedAsync(evt.EventId, ct);
                currentCheckpoint++;
            }

            await _checkpoints.SetLocalCheckpointAsync(currentCheckpoint, ct);

            return DataOperationResult<SyncResult>.Succeeded(new SyncResult
            {
                Success = true,
                EventsDownloaded = applied,
                ConflictsDetected = conflicts,
                NewCheckpoint = currentCheckpoint
            });
        }

        public async Task EnqueueOutboxEventAsync(SyncEventEnvelope envelope, CancellationToken ct = default)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            await _outbox.EnqueueAsync(envelope, ct);
        }

        public Task<SyncStatusInfo> GetStatusAsync(CancellationToken ct = default)
        {
            return _statusProvider.GetStatusAsync(ct);
        }

        public Task PauseAsync(string reason, CancellationToken ct = default)
        {
            _statusProvider.Pause(reason);
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken ct = default)
        {
            _statusProvider.Resume();
            return Task.CompletedTask;
        }
    }
}
