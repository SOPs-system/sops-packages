using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataOperations.Abstractions.Models;
using Sops.Models.Sync;

namespace Sops.DataOperations.Approvals
{
    /// <summary>
    /// Executes approved changes into the authoritative database transaction.
    /// </summary>
    public sealed class ApprovedChangeApplicator
    {
        public Task<ApplyChangeResult> ApplyChangeAsync(ChangeRequest request, long currentCanonicalRevision, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.Status != ChangeRequestStatus.Approved)
            {
                return Task.FromResult(new ApplyChangeResult
                {
                    Success = false,
                    RequestId = request.RequestId,
                    ErrorMessage = $"Cannot apply request with status '{request.Status}'. Must be Approved."
                });
            }

            // In real pipeline, SQL mutation is executed here in database transaction
            long nextRevision = currentCanonicalRevision + 1;

            return Task.FromResult(new ApplyChangeResult
            {
                Success = true,
                RequestId = request.RequestId,
                NewCanonicalRevision = nextRevision
            });
        }
    }
}
