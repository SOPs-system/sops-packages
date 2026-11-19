using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.DataOperations.Sync
{
    /// <summary>
    /// Coordinates persistent synchronization checkpoints and recovery generations.
    /// Prevents sequence collisions and stale event duplication following database restoration.
    /// </summary>
    public sealed class SyncCheckpointCoordinator
    {
        private readonly object _lock = new object();
        private long _localCheckpoint;
        private long _cloudCheckpoint;
        private Guid _recoveryGeneration;

        public SyncCheckpointCoordinator(Guid? initialRecoveryGeneration = null, long initialLocalCheckpoint = 0)
        {
            _recoveryGeneration = initialRecoveryGeneration ?? Guid.NewGuid();
            _localCheckpoint = initialLocalCheckpoint;
            _cloudCheckpoint = initialLocalCheckpoint;
        }

        public long LocalCheckpoint => _localCheckpoint;
        public long CloudCheckpoint => _cloudCheckpoint;
        public Guid RecoveryGeneration => _recoveryGeneration;

        public Task<long> GetLocalCheckpointAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_localCheckpoint);
            }
        }

        public Task SetLocalCheckpointAsync(long checkpoint, CancellationToken ct = default)
        {
            lock (_lock)
            {
                _localCheckpoint = Math.Max(_localCheckpoint, checkpoint);
            }
            return Task.CompletedTask;
        }

        public Task SetCloudCheckpointAsync(long checkpoint, CancellationToken ct = default)
        {
            lock (_lock)
            {
                _cloudCheckpoint = checkpoint;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Rotates the recovery generation after database restoration or recovery reconciliation.
        /// </summary>
        public Guid RotateRecoveryGeneration()
        {
            lock (_lock)
            {
                _recoveryGeneration = Guid.NewGuid();
                return _recoveryGeneration;
            }
        }
    }
}
