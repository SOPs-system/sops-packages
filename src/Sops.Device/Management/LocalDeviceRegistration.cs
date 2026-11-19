using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Locally cached snapshot of device registration status used for offline-first workstation operation.
    /// Explicitly separates local installation identity, last-known cached status, authoritative server status,
    /// and last synchronization timestamp.
    /// </summary>
    public sealed class LocalDeviceRegistration
    {
        /// <summary>Default offline grace period allowed before re-authentication is strictly enforced.</summary>
        public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromDays(14);

        /// <summary>Persistent local installation GUID.</summary>
        public string InstallationId { get; }

        /// <summary>Crockford Base32 device support code.</summary>
        public string DeviceCode { get; }

        /// <summary>Authoritative server registration identifier, if registered.</summary>
        public string? RegistrationId { get; }

        /// <summary>Last known registration status confirmed with the server.</summary>
        public DeviceStatus LastKnownStatus { get; }

        /// <summary>Cryptographic server registration token or signature proof.</summary>
        public string? ServerRegistrationToken { get; }

        /// <summary>Timestamp in UTC of the most recent successful server synchronization or heartbeat.</summary>
        public DateTimeOffset? LastSyncAtUtc { get; }

        /// <summary>Timestamp in UTC when this local registration record was cached.</summary>
        public DateTimeOffset CachedAtUtc { get; }

        /// <summary>Associated pharmacy tenant identifier.</summary>
        public string? TenantId { get; }

        /// <summary>Associated branch identifier.</summary>
        public string? BranchId { get; }

        /// <summary>Whether the workstation is currently known to be connected and reachable to the server.</summary>
        public bool IsConnectedToServer { get; }

        public LocalDeviceRegistration(
            string installationId,
            string deviceCode,
            string? registrationId = null,
            DeviceStatus lastKnownStatus = DeviceStatus.Unregistered,
            string? serverRegistrationToken = null,
            DateTimeOffset? lastSyncAtUtc = null,
            DateTimeOffset? cachedAtUtc = null,
            string? tenantId = null,
            string? branchId = null,
            bool isConnectedToServer = false)
        {
            InstallationId = !string.IsNullOrWhiteSpace(installationId)
                ? installationId.Trim()
                : throw new ArgumentException("InstallationId is required.", nameof(installationId));
            DeviceCode = !string.IsNullOrWhiteSpace(deviceCode)
                ? deviceCode.Trim()
                : throw new ArgumentException("DeviceCode is required.", nameof(deviceCode));
            RegistrationId = registrationId?.Trim();
            LastKnownStatus = lastKnownStatus;
            ServerRegistrationToken = serverRegistrationToken?.Trim();
            LastSyncAtUtc = lastSyncAtUtc;
            CachedAtUtc = cachedAtUtc ?? DateTimeOffset.UtcNow;
            TenantId = tenantId?.Trim();
            BranchId = branchId?.Trim();
            IsConnectedToServer = isConnectedToServer;
        }

        /// <summary>
        /// Evaluates the complete offline status of the device against a configurable offline policy and connection state.
        /// Distinguishes unverified devices, grace periods, expired grace, suspensions, clock rollback, and permanent revocations.
        /// </summary>
        public DeviceOfflineState EvaluateOfflineState(DateTimeOffset nowUtc, DeviceOfflinePolicy? policy = null, bool? isConnected = null)
        {
            if (LastKnownStatus == DeviceStatus.Revoked)
            {
                return DeviceOfflineState.Revoked;
            }

            if (LastKnownStatus == DeviceStatus.Suspended)
            {
                return DeviceOfflineState.Suspended;
            }

            if (LastKnownStatus == DeviceStatus.Unregistered || string.IsNullOrEmpty(RegistrationId))
            {
                return DeviceOfflineState.NeverRegistered;
            }

            if (string.IsNullOrEmpty(ServerRegistrationToken))
            {
                return DeviceOfflineState.CredentialsInvalid;
            }

            var effPolicy = policy ?? new DeviceOfflinePolicy();

            if (!LastSyncAtUtc.HasValue)
            {
                return DeviceOfflineState.OfflineGraceExpired;
            }

            // Check if clock rollback occurred (current time is prior to last sync minus tolerance)
            if (nowUtc < (LastSyncAtUtc.Value - effPolicy.MaxClockRollbackTolerance))
            {
                // Significant clock rollback detected
                return DeviceOfflineState.ClockRollbackDetected;
            }

            bool online = isConnected ?? IsConnectedToServer;

            if (!effPolicy.AllowOfflineOperations)
            {
                // Zero offline tolerance policy
                return (online && (nowUtc - LastSyncAtUtc.Value) <= TimeSpan.FromMinutes(10))
                    ? DeviceOfflineState.ActiveOnline
                    : DeviceOfflineState.OfflineGraceExpired;
            }

            TimeSpan timeSinceSync = nowUtc - LastSyncAtUtc.Value;

            if (online)
            {
                if (timeSinceSync <= TimeSpan.FromMinutes(15))
                {
                    return DeviceOfflineState.ActiveOnline;
                }

                return (timeSinceSync <= effPolicy.GracePeriod)
                    ? DeviceOfflineState.WithinGracePeriod
                    : DeviceOfflineState.OfflineGraceExpired;
            }

            // Disconnected: evaluate against configured grace period
            return (timeSinceSync <= effPolicy.GracePeriod)
                ? DeviceOfflineState.WithinGracePeriod
                : DeviceOfflineState.OfflineGraceExpired;
        }

        /// <summary>
        /// Evaluates whether the device is within its permitted offline grace window.
        /// Returns <c>true</c> if the device was previously verified as Active and less time
        /// than <paramref name="maxGracePeriod"/> has elapsed since the last authoritative sync.
        /// </summary>
        public bool IsWithinOfflineGracePeriod(DateTimeOffset nowUtc, TimeSpan? maxGracePeriod = null)
        {
            var policy = maxGracePeriod.HasValue ? DeviceOfflinePolicy.Custom(maxGracePeriod.Value) : new DeviceOfflinePolicy();
            var state = EvaluateOfflineState(nowUtc, policy);
            return state == DeviceOfflineState.ActiveOnline || state == DeviceOfflineState.WithinGracePeriod;
        }

        public static LocalDeviceRegistration Unregistered(string installationId, string deviceCode) =>
            new LocalDeviceRegistration(installationId, deviceCode, lastKnownStatus: DeviceStatus.Unregistered);

        /// <summary>
        /// Returns a copy of the registration with updated network connectivity status.
        /// </summary>
        public LocalDeviceRegistration WithConnectionState(bool isConnected)
        {
            return new LocalDeviceRegistration(
                InstallationId,
                DeviceCode,
                RegistrationId,
                LastKnownStatus,
                ServerRegistrationToken,
                LastSyncAtUtc,
                CachedAtUtc,
                TenantId,
                BranchId,
                isConnected);
        }

        /// <summary>
        /// Updates the local registration with a new sync timestamp and authoritative status.
        /// Enforces monotonic revocation: an active status from an older cached snapshot cannot revert a previously known revocation.
        /// </summary>
        public LocalDeviceRegistration WithSync(DateTimeOffset syncTimeUtc, DeviceStatus status, string? serverToken = null, bool isConnected = true)
        {
            // Monotonic revocation guarantee: once revoked, stale cached active packets cannot un-revoke
            DeviceStatus effectiveStatus = (LastKnownStatus == DeviceStatus.Revoked && status != DeviceStatus.Revoked)
                ? DeviceStatus.Revoked
                : status;

            return new LocalDeviceRegistration(
                InstallationId,
                DeviceCode,
                RegistrationId,
                effectiveStatus,
                serverToken ?? ServerRegistrationToken,
                syncTimeUtc,
                DateTimeOffset.UtcNow,
                TenantId,
                BranchId,
                isConnected);
        }

        public LocalDeviceRegistration WithRegistration(DeviceRecord record, string serverToken)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            return new LocalDeviceRegistration(
                record.InstallationId,
                record.DeviceCode,
                record.RegistrationId,
                record.Status,
                serverToken,
                record.LastSyncAtUtc ?? DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                record.Ownership.TenantId,
                record.Ownership.BranchId,
                isConnectedToServer: true);
        }
    }
}
