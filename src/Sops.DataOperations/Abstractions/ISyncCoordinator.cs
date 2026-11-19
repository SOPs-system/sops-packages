using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;
using Sops.Models.Sync;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Coordinates offline-first bidirectional synchronization between Local SQLCipher, Global PostgreSQL, and Tenant Projections.
    /// </summary>
    public interface ISyncCoordinator
    {
        /// <summary>
        /// Triggers synchronization on demand across the specified scope.
        /// </summary>
        Task<DataOperationResult<SyncResult>> TriggerSyncAsync(SyncScope scope = SyncScope.All, CancellationToken ct = default);

        /// <summary>
        /// Flushes and dispatches pending local outbox events to cloud destinations.
        /// </summary>
        Task<DataOperationResult<SyncResult>> ProcessOutboxAsync(int batchSize = 100, CancellationToken ct = default);

        /// <summary>
        /// Pulls and processes incoming cloud mutations into the local database transaction.
        /// </summary>
        Task<DataOperationResult<SyncResult>> ProcessInboxAsync(CancellationToken ct = default);

        /// <summary>
        /// Enqueues a local domain mutation event into the durable outbox atomically.
        /// </summary>
        Task EnqueueOutboxEventAsync(SyncEventEnvelope envelope, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the real-time synchronization state, connectivity, and counts.
        /// </summary>
        Task<SyncStatusInfo> GetStatusAsync(CancellationToken ct = default);

        /// <summary>
        /// Temporarily pauses synchronization processing.
        /// </summary>
        Task PauseAsync(string reason, CancellationToken ct = default);

        /// <summary>
        /// Resumes normal synchronization processing.
        /// </summary>
        Task ResumeAsync(CancellationToken ct = default);
    }
}
