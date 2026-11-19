using System;

namespace Sops.DataOperations.Abstractions
{
    /// <summary>
    /// Master coordinator orchestrating offline-first sync, change requests, encrypted backups, safe restoration, and health monitoring.
    /// </summary>
    public interface IDataOperationsCoordinator
    {
        /// <summary>Gets the offline-first synchronization coordinator.</summary>
        ISyncCoordinator Sync { get; }

        /// <summary>Gets the change request and approval coordinator.</summary>
        IChangeApprovalCoordinator Approvals { get; }

        /// <summary>Gets the authenticated encrypted backup coordinator.</summary>
        IEncryptedBackupCoordinator Backup { get; }

        /// <summary>Gets the staged safe restore coordinator.</summary>
        IRestoreCoordinator Restore { get; }

        /// <summary>Gets the operational status and progress provider.</summary>
        IOperationsStatusProvider Status { get; }
    }
}
