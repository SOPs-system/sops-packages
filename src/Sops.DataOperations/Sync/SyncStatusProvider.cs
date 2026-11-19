using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Sync.Storage;

namespace Sops.DataOperations.Sync
{
    /// <summary>
    /// Thread-safe provider reporting real-time synchronization metrics and states.
    /// </summary>
    public sealed class SyncStatusProvider
    {
        private readonly IOutboxStore _outbox;
        private readonly IInboxStore _inbox;
        private readonly SyncCheckpointCoordinator _checkpoints;
        private readonly object _lock = new object();

        private SyncState _state = SyncState.Idle;
        private DateTimeOffset? _lastSuccessfulSyncUtc;
        private int _retryCount;
        private int _failedEventCount;
        private string? _lastErrorSummary;
        private bool _globalConnectivity = true;
        private bool _tenantConnectivity = true;
        private string? _pauseReason;

        public SyncStatusProvider(
            IOutboxStore outbox,
            IInboxStore inbox,
            SyncCheckpointCoordinator checkpoints)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        }

        public SyncState CurrentState => _state;

        public void SetState(SyncState state)
        {
            lock (_lock)
            {
                _state = state;
            }
        }

        public void RecordSuccess()
        {
            lock (_lock)
            {
                _state = SyncState.Idle;
                _lastSuccessfulSyncUtc = DateTimeOffset.UtcNow;
                _retryCount = 0;
                _lastErrorSummary = null;
            }
        }

        public void RecordFailure(string error)
        {
            lock (_lock)
            {
                _state = SyncState.Error;
                _retryCount++;
                _failedEventCount++;
                _lastErrorSummary = error;
            }
        }

        public void SetConnectivity(bool global, bool tenant)
        {
            lock (_lock)
            {
                _globalConnectivity = global;
                _tenantConnectivity = tenant;
                if (!global)
                {
                    _state = SyncState.Offline;
                }
            }
        }

        public void Pause(string reason)
        {
            lock (_lock)
            {
                _state = SyncState.Paused;
                _pauseReason = reason;
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (_state == SyncState.Paused)
                {
                    _state = SyncState.Idle;
                    _pauseReason = null;
                }
            }
        }

        public async Task<SyncStatusInfo> GetStatusAsync(CancellationToken ct = default)
        {
            int pendingOutbox = await _outbox.GetPendingCountAsync(ct);
            int pendingInbox = await _inbox.GetPendingCountAsync(ct);
            long lastCheckpoint = await _checkpoints.GetLocalCheckpointAsync(ct);

            lock (_lock)
            {
                return new SyncStatusInfo
                {
                    State = _state,
                    LastSuccessfulSyncUtc = _lastSuccessfulSyncUtc,
                    PendingOutboxCount = pendingOutbox,
                    PendingInboxCount = pendingInbox,
                    RetryCount = _retryCount,
                    FailedEventCount = _failedEventCount,
                    LastErrorSummary = _lastErrorSummary,
                    GlobalConnectivity = _globalConnectivity,
                    TenantConnectivity = _tenantConnectivity,
                    CurrentRecoveryGeneration = _checkpoints.RecoveryGeneration,
                    LastCheckpoint = lastCheckpoint,
                    PauseReason = _pauseReason,
                    LastStatusCheckUtc = DateTimeOffset.UtcNow
                };
            }
        }
    }
}
