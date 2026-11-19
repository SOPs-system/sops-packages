using System;
using System.Security;
using Sops.DataOperations.Abstractions.Models;

namespace Sops.DataOperations.Security
{
    /// <summary>
    /// Explicit permission definitions for the DataOperations module.
    /// </summary>
    public static class Permissions
    {
        public const string SyncRead = "DataOperations.Sync.Read";
        public const string SyncExecute = "DataOperations.Sync.Execute";
        public const string SyncRetry = "DataOperations.Sync.Retry";

        public const string ApprovalRequest = "DataOperations.Approval.Request";
        public const string ApprovalReview = "DataOperations.Approval.Review";
        public const string ApprovalApprove = "DataOperations.Approval.Approve";
        public const string ApprovalReject = "DataOperations.Approval.Reject";

        public const string BackupCreate = "DataOperations.Backup.Create";
        public const string BackupVerify = "DataOperations.Backup.Verify";
        public const string BackupManage = "DataOperations.Backup.Manage";

        public const string RestorePreview = "DataOperations.Restore.Preview";
        public const string RestoreExecute = "DataOperations.Restore.Execute";
        public const string RecoveryResolve = "DataOperations.Recovery.Resolve";
    }

    /// <summary>
    /// Validates operational authorization, separation of duties, and dual approval requirements.
    /// </summary>
    public static class OperationAuthorization
    {
        /// <summary>
        /// Validates that user holds the required permission, throwing SecurityException if missing.
        /// </summary>
        public static void DemandPermission(UserIdentity user, string permission, string operationName)
        {
            if (user == null)
                throw new SecurityException($"Unauthorized: User context required for '{operationName}'.");

            if (!user.HasPermission(permission))
                throw new SecurityException($"Access denied: User '{user.Username}' ({user.UserId}) lacks required permission '{permission}' for '{operationName}'.");
        }

        /// <summary>
        /// Enforces separation of duties: A requester cannot approve or reject their own change request.
        /// </summary>
        public static void DemandSeparationOfDuties(UserIdentity reviewer, Guid requestedByUserId, string operationName)
        {
            if (reviewer == null)
                throw new SecurityException($"Unauthorized: Reviewer context required for '{operationName}'.");

            if (reviewer.UserId == requestedByUserId)
            {
                throw new SecurityException($"Separation of duties violation: User '{reviewer.Username}' ({reviewer.UserId}) cannot review or approve their own change request.");
            }
        }

        /// <summary>
        /// Validates whether a sensitive change request requires dual management approval.
        /// </summary>
        public static bool RequiresDualApproval(ChangeRiskTier riskTier, string changeType)
        {
            if (riskTier == ChangeRiskTier.Critical || riskTier == ChangeRiskTier.High)
                return true;

            if (string.Equals(changeType, "PriceRegulatedOverride", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(changeType, "StockWriteOff", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(changeType, "MasterCatalogueOverride", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
