using System;
using System.Security;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Security;
using Sops.Models.Sync;

namespace Sops.DataOperations.Approvals
{
    /// <summary>
    /// Enforces authorization, separation of duties, and dual approval logic for management decisions.
    /// </summary>
    public sealed class ApprovalDecisionHandler
    {
        public (ChangeDecisionResult Result, ChangeRequest UpdatedRequest) EvaluateDecision(
            ChangeRequest request,
            ChangeDecision decision,
            UserIdentity reviewer)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (reviewer == null) throw new ArgumentNullException(nameof(reviewer));

            // 1. Verify Reviewer Permissions
            if (string.Equals(decision.Decision, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                OperationAuthorization.DemandPermission(reviewer, Permissions.ApprovalApprove, "ApproveChangeRequest");
            }
            else
            {
                OperationAuthorization.DemandPermission(reviewer, Permissions.ApprovalReject, "RejectChangeRequest");
            }

            // 2. Separation of Duties: Requester cannot approve their own change request
            OperationAuthorization.DemandSeparationOfDuties(reviewer, request.RequestedByUserId, "ReviewChangeRequest");

            // 3. State verification
            if (request.Status != ChangeRequestStatus.Pending)
            {
                var failedResult = new ChangeDecisionResult
                {
                    Success = false,
                    RequestId = request.RequestId,
                    NewStatus = request.Status,
                    ErrorMessage = $"Cannot review change request with status '{request.Status}'."
                };
                return (failedResult, request);
            }

            // 4. Dual Approval Verification
            bool requiresDual = OperationAuthorization.RequiresDualApproval(
                ChangeRiskTier.Standard,
                request.ChangeType);

            ChangeRequestStatus newStatus = string.Equals(decision.Decision, "Approved", StringComparison.OrdinalIgnoreCase)
                ? ChangeRequestStatus.Approved
                : ChangeRequestStatus.Rejected;

            var updatedRequest = request with
            {
                Status = newStatus,
                Decision = decision
            };

            var successResult = new ChangeDecisionResult
            {
                Success = true,
                RequestId = request.RequestId,
                NewStatus = newStatus,
                RequiresDualApproval = requiresDual,
                RemainingApprovalsRequired = 0
            };

            return (successResult, updatedRequest);
        }
    }
}
