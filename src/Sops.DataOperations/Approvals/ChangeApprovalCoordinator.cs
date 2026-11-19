using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.Models.Sync;

namespace Sops.DataOperations.Approvals
{
    /// <summary>
    /// Coordinates submission, review, authorization, and execution of sensitive change requests.
    /// </summary>
    public sealed class ChangeApprovalCoordinator : IChangeApprovalCoordinator
    {
        private readonly ConcurrentDictionary<Guid, ChangeRequest> _requests = new ConcurrentDictionary<Guid, ChangeRequest>();
        private readonly ApprovalDecisionHandler _decisionHandler;
        private readonly ChangeRequestDispatcher _dispatcher;
        private readonly ApprovedChangeApplicator _applicator;
        private readonly ILogger<ChangeApprovalCoordinator>? _logger;

        public ChangeApprovalCoordinator(
            ApprovalDecisionHandler decisionHandler,
            ChangeRequestDispatcher dispatcher,
            ApprovedChangeApplicator applicator,
            ILogger<ChangeApprovalCoordinator>? logger = null)
        {
            _decisionHandler = decisionHandler ?? throw new ArgumentNullException(nameof(decisionHandler));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _applicator = applicator ?? throw new ArgumentNullException(nameof(applicator));
            _logger = logger;
        }

        public async Task<DataOperationResult<ChangeRequest>> SubmitRequestAsync(ChangeRequestSubmission submission, CancellationToken ct = default)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));

            if (string.IsNullOrWhiteSpace(submission.EntityType))
                return DataOperationResult<ChangeRequest>.Failed("INVALID_SUBMISSION", "EntityType is required.");

            if (string.IsNullOrWhiteSpace(submission.Reason))
                return DataOperationResult<ChangeRequest>.Failed("INVALID_SUBMISSION", "Reason is required.");

            string changeType = !string.IsNullOrWhiteSpace(submission.ChangeType)
                ? submission.ChangeType
                : (!string.IsNullOrWhiteSpace(submission.OperationType) ? submission.OperationType : "General");

            var request = new ChangeRequest
            {
                RequestId = Guid.NewGuid(),
                TenantId = submission.ChainId ?? Guid.Empty,
                BranchId = submission.PharmacyId,
                RequestedByUserId = submission.RequestedByUserId,
                ChangeType = changeType,
                Reason = submission.Reason,
                Status = ChangeRequestStatus.Pending,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _requests.TryAdd(request.RequestId, request);

            // Dispatch to durable outbox & cloud
            await _dispatcher.DispatchCreatedRequestAsync(request, ct);

            _logger?.LogInformation("Change request {RequestId} ({ChangeType}) submitted by {UserId}.", request.RequestId, request.ChangeType, request.RequestedByUserId);

            return DataOperationResult<ChangeRequest>.Succeeded(request, "Submitted");
        }

        public async Task<DataOperationResult<ChangeDecisionResult>> ReviewRequestAsync(Guid requestId, ChangeDecision decision, UserIdentity reviewer, CancellationToken ct = default)
        {
            if (!_requests.TryGetValue(requestId, out var request))
            {
                return DataOperationResult<ChangeDecisionResult>.Failed("NOT_FOUND", $"Change request '{requestId}' was not found.");
            }

            try
            {
                var (result, updatedRequest) = _decisionHandler.EvaluateDecision(request, decision, reviewer);
                if (!result.Success)
                {
                    return DataOperationResult<ChangeDecisionResult>.Failed("DECISION_REJECTED", result.ErrorMessage ?? "Decision evaluation failed.");
                }

                _requests[requestId] = updatedRequest;

                // Dispatch decision to outbox
                await _dispatcher.DispatchDecisionAsync(updatedRequest, ct);

                _logger?.LogInformation("Change request {RequestId} reviewed by {ReviewerId}: Result={Decision}", requestId, reviewer.UserId, decision.Decision);

                return DataOperationResult<ChangeDecisionResult>.Succeeded(result);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to evaluate decision on request {RequestId}.", requestId);
                return DataOperationResult<ChangeDecisionResult>.Failed("REVIEW_FAILED", ex.Message);
            }
        }

        public Task<IReadOnlyList<ChangeRequest>> GetPendingRequestsAsync(Guid? branchId = null, CancellationToken ct = default)
        {
            var query = _requests.Values.Where(r => r.Status == ChangeRequestStatus.Pending);
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                query = query.Where(r => r.BranchId == branchId.Value);
            }

            return Task.FromResult<IReadOnlyList<ChangeRequest>>(query.OrderByDescending(r => r.CreatedAtUtc).ToList());
        }

        public Task<ChangeRequest?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default)
        {
            _requests.TryGetValue(requestId, out var request);
            return Task.FromResult<ChangeRequest?>(request);
        }

        public async Task<DataOperationResult<ApplyChangeResult>> ApplyApprovedChangeAsync(Guid requestId, CancellationToken ct = default)
        {
            if (!_requests.TryGetValue(requestId, out var request))
            {
                return DataOperationResult<ApplyChangeResult>.Failed("NOT_FOUND", $"Change request '{requestId}' was not found.");
            }

            var applyResult = await _applicator.ApplyChangeAsync(request, 100, ct);
            if (!applyResult.Success)
            {
                return DataOperationResult<ApplyChangeResult>.Failed("APPLY_FAILED", applyResult.ErrorMessage ?? "Failed to apply approved change.");
            }

            _logger?.LogInformation("Approved change request {RequestId} successfully applied. New revision: {Revision}", requestId, applyResult.NewCanonicalRevision);

            return DataOperationResult<ApplyChangeResult>.Succeeded(applyResult);
        }
    }
}
