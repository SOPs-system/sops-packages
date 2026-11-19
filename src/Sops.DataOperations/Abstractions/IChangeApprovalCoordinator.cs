using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;
using Sops.Models.Sync;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Coordinates proposal, review, authorization, and transactional application of sensitive record change requests.
    /// </summary>
    public interface IChangeApprovalCoordinator
    {
        /// <summary>
        /// Proposes a new sensitive change request offline or online.
        /// </summary>
        Task<DataOperationResult<ChangeRequest>> SubmitRequestAsync(ChangeRequestSubmission submission, CancellationToken ct = default);

        /// <summary>
        /// Records an authorized manager review decision (Approve / Reject) with separation of duties enforcement.
        /// </summary>
        Task<DataOperationResult<ChangeDecisionResult>> ReviewRequestAsync(Guid requestId, ChangeDecision decision, UserIdentity reviewer, CancellationToken ct = default);

        /// <summary>
        /// Retrieves pending change requests awaiting manager review.
        /// </summary>
        Task<IReadOnlyList<ChangeRequest>> GetPendingRequestsAsync(Guid? branchId = null, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific change request by its unique identifier.
        /// </summary>
        Task<ChangeRequest?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default);

        /// <summary>
        /// Applies an approved change request within an authoritative database transaction.
        /// </summary>
        Task<DataOperationResult<ApplyChangeResult>> ApplyApprovedChangeAsync(Guid requestId, CancellationToken ct = default);
    }
}
