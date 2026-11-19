using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;
using Sops.Models.Sync;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Transport interface connecting the local node to remote cloud synchronization services.
    /// </summary>
    public interface ISyncTransport
    {
        /// <summary>Checks network connectivity to global cloud services.</summary>
        Task<bool> CheckGlobalConnectivityAsync(CancellationToken ct = default);

        /// <summary>Checks network connectivity to chain tenant services.</summary>
        Task<bool> CheckTenantConnectivityAsync(CancellationToken ct = default);

        /// <summary>Uploads a batch of domain mutation events to the cloud.</summary>
        Task<SyncBatchAck> UploadBatchAsync(SyncBatch batch, CancellationToken ct = default);

        /// <summary>Downloads cloud events occurred since a specific checkpoint.</summary>
        Task<IReadOnlyList<SyncEventEnvelope>> DownloadEventsAsync(Guid pharmacyId, long sinceCheckpoint, int maxCount = 100, CancellationToken ct = default);

        /// <summary>Gets the authoritative cloud checkpoint sequence number for a pharmacy.</summary>
        Task<long> GetCloudCheckpointAsync(Guid pharmacyId, CancellationToken ct = default);

        /// <summary>Dispatches a sensitive change request for remote manager review.</summary>
        Task<bool> DispatchChangeRequestAsync(ChangeRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Durable acknowledgment response returned by cloud sync receivers.
    /// </summary>
    public sealed class SyncBatchAck
    {
        public Guid BatchId { get; set; }
        public bool Success { get; set; }
        public long CommittedCheckpoint { get; set; }
        public int AcknowledgedEventsCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
