using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.Models.Sync;

namespace Sops.DataOperations.Sync
{
    /// <summary>
    /// Default no-op transport when an external cloud synchronization transport is not explicitly registered.
    /// </summary>
    public sealed class NullSyncTransport : ISyncTransport
    {
        public Task<bool> CheckGlobalConnectivityAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> CheckTenantConnectivityAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task<SyncBatchAck> UploadBatchAsync(SyncBatch batch, CancellationToken ct = default) =>
            Task.FromResult(new SyncBatchAck
            {
                BatchId = batch?.BatchId ?? Guid.Empty,
                Success = false,
                ErrorMessage = "No remote cloud sync transport registered."
            });

        public Task<IReadOnlyList<SyncEventEnvelope>> DownloadEventsAsync(Guid pharmacyId, long sinceCheckpoint, int maxCount = 100, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SyncEventEnvelope>>(new List<SyncEventEnvelope>());

        public Task<long> GetCloudCheckpointAsync(Guid pharmacyId, CancellationToken ct = default) => Task.FromResult(0L);

        public Task<bool> DispatchChangeRequestAsync(ChangeRequest request, CancellationToken ct = default) => Task.FromResult(false);
    }
}
