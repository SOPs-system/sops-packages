using System;
using System.Collections.Generic;
using Sops.Models.Sync;

namespace Sops.DataOperations.Abstractions.Models
{
    /// <summary>
    /// Risk tier for sensitive operations requiring management review.
    /// </summary>
    public enum ChangeRiskTier
    {
        Standard = 0,
        Elevated = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Submission payload to propose a sensitive change request.
    /// </summary>
    public sealed class ChangeRequestSubmission
    {
        public Guid PharmacyId { get; set; }
        public Guid? ChainId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public long BaseRevision { get; set; }
        public string ProposedChangesJson { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public Guid RequestedByUserId { get; set; }
        public string RequestedByUsername { get; set; } = string.Empty;
        public ChangeRiskTier RiskClassification { get; set; } = ChangeRiskTier.Standard;
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Outcome of a change review action (approve/reject).
    /// </summary>
    public sealed class ChangeDecisionResult
    {
        public bool Success { get; set; }
        public Guid RequestId { get; set; }
        public ChangeRequestStatus NewStatus { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RequiresDualApproval { get; set; }
        public int RemainingApprovalsRequired { get; set; }
    }

    /// <summary>
    /// Result of applying an approved change to the authoritative database.
    /// </summary>
    public sealed class ApplyChangeResult
    {
        public bool Success { get; set; }
        public Guid RequestId { get; set; }
        public long NewCanonicalRevision { get; set; }
        public string? ErrorMessage { get; set; }
        public bool ConflictDetected { get; set; }
    }
}
