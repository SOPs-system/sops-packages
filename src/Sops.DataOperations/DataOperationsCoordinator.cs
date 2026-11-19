using System;
using Sops.DataOperations.Abstractions;

namespace Sops.DataOperations
{
    /// <summary>
    /// Master unified entry point for synchronization, change approvals, encrypted backups, safe restoration, and health monitoring.
    /// </summary>
    public sealed class DataOperationsCoordinator : IDataOperationsCoordinator
    {
        public ISyncCoordinator Sync { get; }
        public IChangeApprovalCoordinator Approvals { get; }
        public IEncryptedBackupCoordinator Backup { get; }
        public IRestoreCoordinator Restore { get; }
        public IOperationsStatusProvider Status { get; }

        public DataOperationsCoordinator(
            ISyncCoordinator sync,
            IChangeApprovalCoordinator approvals,
            IEncryptedBackupCoordinator backup,
            IRestoreCoordinator restore,
            IOperationsStatusProvider status)
        {
            Sync = sync ?? throw new ArgumentNullException(nameof(sync));
            Approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
            Backup = backup ?? throw new ArgumentNullException(nameof(backup));
            Restore = restore ?? throw new ArgumentNullException(nameof(restore));
            Status = status ?? throw new ArgumentNullException(nameof(status));
        }
    }
}
