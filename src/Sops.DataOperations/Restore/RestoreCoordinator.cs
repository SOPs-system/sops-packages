using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Abstractions.Models;
using Sops.DataOperations.Security;

namespace Sops.DataOperations.Restore
{
    /// <summary>
    /// Coordinates the multi-step verified database restore lifecycle, maintenance locking, rollback, and post-restore reconciliation.
    /// </summary>
    public sealed class RestoreCoordinator : IRestoreCoordinator
    {
        private readonly RestorePreflight _preflight;
        private readonly RestoreStagingService _stagingService;
        private readonly RestoreActivationService _activationService;
        private readonly PostRestoreSyncCoordinator _postRestoreSync;
        private readonly ILogger<RestoreCoordinator>? _logger;

        public RestoreCoordinator(
            RestorePreflight preflight,
            RestoreStagingService stagingService,
            RestoreActivationService activationService,
            PostRestoreSyncCoordinator postRestoreSync,
            ILogger<RestoreCoordinator>? logger = null)
        {
            _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
            _stagingService = stagingService ?? throw new ArgumentNullException(nameof(stagingService));
            _activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
            _postRestoreSync = postRestoreSync ?? throw new ArgumentNullException(nameof(postRestoreSync));
            _logger = logger;
        }

        public Task<RestorePreview> PreflightCheckAsync(RestoreRequest request, CancellationToken ct = default)
        {
            return _preflight.CheckAsync(request, ct);
        }

        public async Task<DataOperationResult<RestoreExecutionResult>> ExecuteRestoreAsync(
            RestoreRequest request,
            UserIdentity administrator,
            IProgress<OperationProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (administrator == null) throw new ArgumentNullException(nameof(administrator));

            var stopwatch = Stopwatch.StartNew();

            // 1. Authorize Administrator
            try
            {
                OperationAuthorization.DemandPermission(administrator, Permissions.RestoreExecute, "ExecuteRestore");
            }
            catch (Exception authEx)
            {
                return DataOperationResult<RestoreExecutionResult>.Failed("UNAUTHORIZED", authEx.Message);
            }

            // 2. Preflight Check
            var preview = await _preflight.CheckAsync(request, ct);
            if (!preview.IsValid)
            {
                string errors = string.Join("; ", preview.Errors);
                return DataOperationResult<RestoreExecutionResult>.Failed("PREFLIGHT_FAILED", $"Preflight checks failed: {errors}");
            }

            if (request.DryRunOnly)
            {
                stopwatch.Stop();
                return DataOperationResult<RestoreExecutionResult>.Succeeded(new RestoreExecutionResult
                {
                    Success = true,
                    FinalStep = RestoreStep.Preflight,
                    Manifest = preview.Manifest,
                    Duration = stopwatch.Elapsed
                }, "DryRunSucceeded");
            }

            string stagingFile = string.Empty;
            string safetyCopy = string.Empty;
            RestoreStep currentStep = RestoreStep.Preflight;

            try
            {
                // 3. Staging Decryption & Verification
                currentStep = RestoreStep.Staging;
                stagingFile = await _stagingService.StageDecryptedDatabaseAsync(request, preview.Manifest!, progress, ct);
                currentStep = RestoreStep.IntegrityVerified;

                // 4. Enter Maintenance Mode
                currentStep = RestoreStep.MaintenanceMode;
                _activationService.EnterMaintenanceMode("Database restoration in progress.");

                // 5. Create Pre-Restore Safety Copy
                currentStep = RestoreStep.SafetyCopyCreated;
                safetyCopy = _activationService.CreatePreRestoreSafetyCopy(request.TargetDatabasePath);

                // 6. Atomic Activation Swap
                currentStep = RestoreStep.Activated;
                _activationService.ActivateStagedDatabase(stagingFile, request.TargetDatabasePath, safetyCopy);

                // 7. Post-Restore Sync Reconciliation
                currentStep = RestoreStep.ReconciliationPending;
                var reconciliation = await _postRestoreSync.ReconcileAsync(
                    preview.Manifest!.PharmacyId,
                    preview.Manifest.SnapshotCheckpoint,
                    preview.Manifest.RecoveryGeneration,
                    ct);

                // 8. Exit Maintenance Mode
                _activationService.ExitMaintenanceMode();
                currentStep = RestoreStep.Completed;
                stopwatch.Stop();

                _logger?.LogInformation("Restore completed successfully in {Elapsed}. Recovery generation rotated.", stopwatch.Elapsed);

                var executionResult = new RestoreExecutionResult
                {
                    Success = true,
                    FinalStep = RestoreStep.Completed,
                    Manifest = preview.Manifest,
                    PreRestoreSafetyCopyPath = safetyCopy,
                    Reconciliation = reconciliation,
                    Duration = stopwatch.Elapsed
                };

                return DataOperationResult<RestoreExecutionResult>.Succeeded(executionResult);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Restore failed at step {Step}! Rolling back if required...", currentStep);

                bool rolledBack = false;
                if (currentStep == RestoreStep.Activated || currentStep == RestoreStep.ReconciliationPending)
                {
                    _activationService.Rollback(safetyCopy, request.TargetDatabasePath);
                    rolledBack = true;
                }

                _activationService.ExitMaintenanceMode();
                stopwatch.Stop();

                var failedResult = new RestoreExecutionResult
                {
                    Success = false,
                    FinalStep = rolledBack ? RestoreStep.RolledBack : RestoreStep.Failed,
                    Manifest = preview.Manifest,
                    PreRestoreSafetyCopyPath = safetyCopy,
                    ErrorMessage = ex.Message,
                    RolledBack = rolledBack,
                    Duration = stopwatch.Elapsed
                };

                return DataOperationResult<RestoreExecutionResult>.Failed("RESTORE_FAILED", ex.Message);
            }
        }

        public async Task<DataOperationResult<ReconciliationResult>> ReconcilePostRestoreAsync(
            Guid pharmacyId,
            long restoredCheckpoint,
            Guid restoredRecoveryGeneration,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _postRestoreSync.ReconcileAsync(pharmacyId, restoredCheckpoint, restoredRecoveryGeneration, ct);
                return DataOperationResult<ReconciliationResult>.Succeeded(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Post-restore reconciliation failed for pharmacy {PharmacyId}.", pharmacyId);
                return DataOperationResult<ReconciliationResult>.Failed("RECONCILIATION_FAILED", ex.Message);
            }
        }
    }
}
