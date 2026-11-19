using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Models;
using Sops.Device.Persistence;

namespace Sops.Device.Management
{
    /// <summary>
    /// Client-side coordinator managing workstation registration lifecycle, offline caching, and server reconciliation.
    /// Distinguishes local installation identity from server registration status and enforces configurable offline grace policy.
    /// </summary>
    public sealed class LocalDeviceRegistrationManager
    {
        private readonly IDeviceService _deviceService;
        private readonly IDeviceCredentialStore? _credentialStore;
        private readonly DeviceOptions _options;
        private readonly ILogger<LocalDeviceRegistrationManager>? _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>Configurable policy governing offline operation and grace duration.</summary>
        public DeviceOfflinePolicy OfflinePolicy { get; set; }

        private LocalDeviceRegistration? _currentRegistration;

        public LocalDeviceRegistrationManager(
            IDeviceService deviceService,
            DeviceOptions options,
            IDeviceCredentialStore? credentialStore = null,
            DeviceOfflinePolicy? offlinePolicy = null,
            ILogger<LocalDeviceRegistrationManager>? logger = null)
        {
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _credentialStore = credentialStore;
            OfflinePolicy = offlinePolicy ?? new DeviceOfflinePolicy();
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the current local device registration state.
        /// Initializes an Unregistered state if the device has not yet completed server registration.
        /// </summary>
        public async Task<LocalDeviceRegistration> GetCurrentStateAsync(CancellationToken cancellationToken = default)
        {
            if (_currentRegistration != null) return _currentRegistration;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_currentRegistration != null) return _currentRegistration;

                // Try reading securely persisted credentials if credential store is configured
                if (_credentialStore != null)
                {
                    try
                    {
                        var stored = await _credentialStore.ReadRegistrationAsync(cancellationToken).ConfigureAwait(false);
                        if (stored != null)
                        {
                            _currentRegistration = stored;
                            return _currentRegistration;
                        }
                    }
                    catch (DeviceCredentialStorageException ex) when (ex.IsCredentialLost)
                    {
                        _logger?.LogError(ex, "Device credentials could not be decrypted or are corrupted. Credential loss detected.");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to read device credentials from store; falling back to uninitialized state.");
                    }
                }

                var identity = await _deviceService.GetIdentityAsync(cancellationToken).ConfigureAwait(false);
                _currentRegistration = LocalDeviceRegistration.Unregistered(identity.InstallationId, identity.DeviceCode);
                return _currentRegistration;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Registers the device with the provided registry using a deterministic idempotency key.
        /// Retries safely replay without creating duplicate records on the server.
        /// </summary>
        public async Task<DeviceRegistrationResult> RegisterAsync(
            IDeviceRegistry registry,
            DeviceOwnership ownership,
            string? displayName = null,
            CancellationToken cancellationToken = default)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var identity = await _deviceService.GetIdentityAsync(cancellationToken).ConfigureAwait(false);
                var deviceInfo = await _deviceService.GetDeviceInfoAsync(cancellationToken).ConfigureAwait(false);

                // Derive stable idempotency token from installation ID and tenant to ensure replay safety across retries
                string idempotencyToken = $"reg:{identity.InstallationId}:{ownership.TenantId}:v1";

                var metadata = new DeviceMetadata(
                    hostname: Environment.MachineName,
                    osPlatform: identity.Platform,
                    osVersion: deviceInfo.PlatformVersion,
                    appVersion: deviceInfo.AppVersion,
                    architecture: deviceInfo.Architecture);

                var request = new DeviceRegistrationRequest(
                    idempotencyToken: idempotencyToken,
                    deviceCode: identity.DeviceCode,
                    installationId: identity.InstallationId,
                    displayName: displayName ?? $"{identity.DeviceCode} - POS",
                    platform: identity.Platform,
                    ownership: ownership,
                    metadata: metadata,
                    fingerprint: identity.Fingerprint,
                    requestedAtUtc: DateTimeOffset.UtcNow);

                var result = await registry.RegisterAsync(request, cancellationToken).ConfigureAwait(false);

                if (result.Success && result.Record != null && result.ServerToken != null)
                {
                    _currentRegistration = new LocalDeviceRegistration(
                        identity.InstallationId,
                        identity.DeviceCode,
                        result.RegistrationId,
                        result.Status,
                        result.ServerToken,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        ownership.TenantId,
                        ownership.BranchId,
                        isConnectedToServer: true);

                    _logger?.LogInformation("Device {Code} registered successfully with ID {RegId}", identity.DeviceCode, result.RegistrationId);
                }
                else if (result.Status == DeviceStatus.Revoked || result.Status == DeviceStatus.Suspended)
                {
                    _currentRegistration = new LocalDeviceRegistration(
                        identity.InstallationId,
                        identity.DeviceCode,
                        result.RegistrationId,
                        result.Status,
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        ownership.TenantId,
                        ownership.BranchId,
                        isConnectedToServer: true);

                    _logger?.LogWarning("Device registration was rejected by server with status {Status}: {Message}", result.Status, result.ErrorMessage);
                }

                if (_credentialStore != null && _currentRegistration != null)
                {
                    try
                    {
                        await _credentialStore.SaveRegistrationAsync(_currentRegistration, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to persist updated registration credentials to store.");
                    }
                }

                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Sends a heartbeat to the server and reconciles local state with authoritative server status.
        /// If network is unreachable, checks offline grace period without wiping credentials.
        /// </summary>
        public async Task<DeviceHeartbeatResult> SendHeartbeatAsync(
            IDeviceRegistry registry,
            long sequenceNumber = 1,
            CancellationToken cancellationToken = default)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var current = await GetCurrentStateAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(current.RegistrationId))
            {
                return new DeviceHeartbeatResult(
                    acknowledged: false,
                    status: DeviceStatus.Unregistered,
                    instructions: "Device is not yet registered on this workstation.");
            }

            try
            {
                var heartbeat = new DeviceHeartbeat(
                    registrationId: current.RegistrationId!,
                    deviceCode: current.DeviceCode,
                    installationId: current.InstallationId,
                    sequenceNumber: sequenceNumber,
                    appVersion: _options.AppVersion,
                    timestampUtc: DateTimeOffset.UtcNow);

                var result = await registry.ReportHeartbeatAsync(heartbeat, cancellationToken).ConfigureAwait(false);

                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // Reconcile authoritative status with live connection
                    _currentRegistration = current.WithSync(result.ServerTimestampUtc, result.Status, isConnected: true);

                    if (result.RequiresRevocation)
                    {
                        _logger?.LogWarning("Server instructed device revocation/suspension during heartbeat. Current status: {Status}", result.Status);
                    }

                    if (_credentialStore != null)
                    {
                        try
                        {
                            await _credentialStore.SaveRegistrationAsync(_currentRegistration, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to persist heartbeat state to credential store.");
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Heartbeat communication failed. Evaluating offline grace period.");

                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    _currentRegistration = current.WithConnectionState(isConnected: false);
                }
                finally
                {
                    _semaphore.Release();
                }

                // Network failure: check if within permitted offline grace window
                bool withinGrace = _currentRegistration.IsWithinOfflineGracePeriod(DateTimeOffset.UtcNow, OfflinePolicy.GracePeriod);
                return new DeviceHeartbeatResult(
                    acknowledged: false,
                    status: current.LastKnownStatus,
                    serverTimestampUtc: DateTimeOffset.UtcNow,
                    requiresSync: false,
                    instructions: withinGrace
                        ? "Offline grace mode active. Server currently unreachable."
                        : "Offline grace period expired. Reconnection to server required.");
            }
        }
    }
}
