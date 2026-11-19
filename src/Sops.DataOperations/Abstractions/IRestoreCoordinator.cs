using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Coordinates staged, safe database restoration, maintenance locking, pre-restore safety copies, rollback protection, and post-restore cloud reconciliation.
    /// </summary>
    public interface IRestoreCoordinator
    {
        /// <summary>
        /// Performs preflight inspection and dry-run validation of a backup archive before touching live database state.
        /// </summary>
        Task<RestorePreview> PreflightCheckAsync(RestoreRequest request, CancellationToken ct = default);

        /// <summary>
        /// Executes the staged restore lifecycle: preflight, staging verification, maintenance mode, safety copy, atomic activation, and rollback on failure.
        /// </summary>
        Task<DataOperationResult<RestoreExecutionResult>> ExecuteRestoreAsync(RestoreRequest request, UserIdentity administrator, IProgress<OperationProgressReport>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Reconciles synchronized cloud events, outbox/inbox discrepancies, and rotates recovery generations after restoration.
        /// </summary>
        Task<DataOperationResult<ReconciliationResult>> ReconcilePostRestoreAsync(Guid pharmacyId, long restoredCheckpoint, Guid restoredRecoveryGeneration, CancellationToken ct = default);
    }
}
