using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Sync;
using Sops.DataOperations.Sync.Storage;

namespace Sops.DataOperations.Restore
{
    /// <summary>
    /// Reconciles synchronized cloud events, outbox/inbox discrepancies, and rotates recovery generations following database restoration.
    /// </summary>
    public sealed class PostRestoreSyncCoordinator
    {
        private readonly SyncCheckpointCoordinator _checkpoints;
        private readonly IOutboxStore _outbox;
        private readonly IInboxStore _inbox;
        private readonly ISyncTransport _transport;
        private readonly SyncConflictHandler _conflicts;
        private readonly ILogger<PostRestoreSyncCoordinator>? _logger;

        public PostRestoreSyncCoordinator(
            SyncCheckpointCoordinator checkpoints,
            IOutboxStore outbox,
            IInboxStore inbox,
            ISyncTransport transport,
            SyncConflictHandler conflicts,
            ILogger<PostRestoreSyncCoordinator>? logger = null)
        {
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _conflicts = conflicts ?? throw new ArgumentNullException(nameof(conflicts));
            _logger = logger;
        }

        public async Task<ReconciliationResult> ReconcileAsync(
            Guid pharmacyId,
            long restoredCheckpoint,
            Guid restoredRecoveryGeneration,
            CancellationToken ct = default)
        {
            // 1. Mandatory Recovery-Generation Rotation: prevents sequence number collisions
            Guid previousGen = restoredRecoveryGeneration;
            Guid newGen = _checkpoints.RotateRecoveryGeneration();

            _logger?.LogInformation("Recovery generation rotated: Previous={PrevGen}, New={NewGen}", previousGen, newGen);

            // 2. Fetch authoritative cloud checkpoint
            long cloudCheckpoint = await _transport.GetCloudCheckpointAsync(pharmacyId, ct);
            _logger?.LogInformation("Post-restore checkpoint comparison: Restored={Restored}, Cloud={Cloud}", restoredCheckpoint, cloudCheckpoint);

            int missingEventsRecovered = 0;
            int duplicateEventsSkipped = 0;
            int quarantinedConflicts = 0;

            // 3. Reconcile missing cloud events if cloud is ahead of restored backup
            if (cloudCheckpoint > restoredCheckpoint)
            {
                long gapCount = cloudCheckpoint - restoredCheckpoint;
                _logger?.LogWarning("Restored database is behind cloud by {Count} checkpoints. Recovering cloud events...", gapCount);

                var eventsToReplay = await _transport.DownloadEventsAsync(pharmacyId, restoredCheckpoint, (int)Math.Min(gapCount, 500), ct);
                foreach (var evt in eventsToReplay)
                {
                    ct.ThrowIfCancellationRequested();

                    if (await _inbox.HasEventBeenProcessedAsync(evt.EventId, ct))
                    {
                        duplicateEventsSkipped++;
                        continue;
                    }

                    await _inbox.EnqueueAsync(evt, ct);
                    await _inbox.MarkProcessedAsync(evt.EventId, ct);
                    missingEventsRecovered++;
                }

                await _checkpoints.SetLocalCheckpointAsync(cloudCheckpoint, ct);
            }
            else
            {
                await _checkpoints.SetLocalCheckpointAsync(restoredCheckpoint, ct);
            }

            // 4. Inspect local outbox for orphaned unacknowledged records
            int orphanedOutbox = await _outbox.GetPendingCountAsync(ct);
            if (orphanedOutbox > 0)
            {
                _logger?.LogWarning("Restored database contains {Count} pending outbox records.", orphanedOutbox);
            }

            var status = quarantinedConflicts > 0
                ? ReconciliationStatus.ReconciledWithQuarantine
                : ReconciliationStatus.ReconciledClean;

            var result = new ReconciliationResult
            {
                PharmacyId = pharmacyId,
                PreviousRecoveryGeneration = previousGen,
                NewRecoveryGeneration = newGen,
                RestoredCheckpoint = restoredCheckpoint,
                CloudCheckpoint = cloudCheckpoint,
                DuplicateEventsCount = duplicateEventsSkipped,
                MissingCloudEventsRecovered = missingEventsRecovered,
                LocalOutboxOrphanedCount = orphanedOutbox,
                QuarantinedConflictsCount = quarantinedConflicts,
                Status = status,
                Details = $"Reconciliation complete. Generation rotated to {newGen:N}. {missingEventsRecovered} events recovered from cloud, {duplicateEventsSkipped} duplicates skipped."
            };

            return result;
        }
    }
}
