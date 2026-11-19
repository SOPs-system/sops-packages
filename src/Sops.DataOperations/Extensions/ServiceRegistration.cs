using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Approvals;
using Sops.DataOperations.Backup;
using Sops.DataOperations.Diagnostics;
using Sops.DataOperations.Restore;
using Sops.DataOperations.Sync;
using Sops.DataOperations.Sync.Storage;

namespace Sops.DataOperations.Extensions
{
    /// <summary>
    /// Extension methods to register Sops.DataOperations services with the dependency injection container.
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Adds Sops.DataOperations unified sync, approval, encrypted backup, and restore services to the service collection.
        /// </summary>
        public static IServiceCollection AddSopsDataOperations(this IServiceCollection services, Action<DataOperationsOptions>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var options = new DataOperationsOptions();
            configure?.Invoke(options);

            // Register Options
            services.TryAddSingleton(options);
            services.TryAddSingleton(options.SyncPolicy);
            services.TryAddSingleton(options.BackupPolicy);

            // Sync Stores & Coordinators
            services.TryAddSingleton<ISyncTransport, NullSyncTransport>();
            services.TryAddSingleton<InMemorySyncStore>();
            services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemorySyncStore>());
            services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<InMemorySyncStore>());
            services.TryAddSingleton<SyncCheckpointCoordinator>();
            services.TryAddSingleton<SyncConflictHandler>();
            services.TryAddSingleton<SyncStatusProvider>();

            // Approvals
            services.TryAddSingleton<ApprovalDecisionHandler>();
            services.TryAddSingleton<ApprovedChangeApplicator>();
            services.TryAddSingleton<ChangeRequestDispatcher>();
            services.TryAddSingleton<IChangeApprovalCoordinator, ChangeApprovalCoordinator>();

            // Backup
            services.TryAddSingleton<BackupVerificationService>();
            services.TryAddSingleton<BackupRetentionManager>();
            services.TryAddSingleton<IEncryptedBackupCoordinator, EncryptedBackupCoordinator>();

            // Restore
            services.TryAddSingleton<RestorePreflight>();
            services.TryAddSingleton<RestoreStagingService>();
            services.TryAddSingleton<RestoreActivationService>();
            services.TryAddSingleton<PostRestoreSyncCoordinator>();
            services.TryAddSingleton<IRestoreCoordinator, RestoreCoordinator>();

            // Diagnostics & Status
            services.TryAddSingleton<OperationsHealthProvider>();
            services.TryAddSingleton<IOperationsStatusProvider>(sp => sp.GetRequiredService<OperationsHealthProvider>());

            // Coordinators
            services.TryAddSingleton<ISyncCoordinator, SyncCoordinator>();
            services.TryAddSingleton<IDataOperationsCoordinator, DataOperationsCoordinator>();

            return services;
        }
    }

    /// <summary>
    /// Consolidated configuration options for Sops.DataOperations.
    /// </summary>
    public sealed class DataOperationsOptions
    {
        public SyncPolicy SyncPolicy { get; set; } = new SyncPolicy();
        public BackupPolicy BackupPolicy { get; set; } = new BackupPolicy();
        public Guid PharmacyId { get; set; }
        public Guid? ChainId { get; set; }
        public string InstallationId { get; set; } = string.Empty;
    }
}
