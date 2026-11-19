using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Device.Management
{
    /// <summary>
    /// Thread-safe in-memory implementation of <see cref="IDeviceRegistry"/> and <see cref="IDeviceManagementService"/>.
    /// Suitable for testing, deterministic evaluation, client-side mocking, and single-node service deployments.
    /// Implements idempotency deduplication, device-code collision detection, and full lifecycle audit trails.
    /// </summary>
    public sealed class InMemoryDeviceRegistry : IDeviceRegistry, IDeviceManagementService
    {
        private readonly object _syncLock = new object();

        private readonly Dictionary<string, DeviceRecord> _devicesById =
            new Dictionary<string, DeviceRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _registrationIdByCode =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DeviceRegistrationResult> _idempotencyResults =
            new Dictionary<string, DeviceRegistrationResult>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> _idempotencySignatures =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<DeviceAuditEvent>> _auditEvents =
            new Dictionary<string, List<DeviceAuditEvent>>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public Task<DeviceRegistrationResult> RegisterAsync(
            DeviceRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            lock (_syncLock)
            {
                string payloadSig = $"{request.InstallationId}:{request.DeviceCode}:{request.Ownership.TenantId}:{request.DisplayName}";

                // 1. Idempotency check: if token was already processed, return idempotent response
                if (_idempotencyResults.TryGetValue(request.IdempotencyToken, out var existingResult))
                {
                    if (_idempotencySignatures.TryGetValue(request.IdempotencyToken, out var existingSig) &&
                        string.Equals(existingSig, payloadSig, StringComparison.Ordinal))
                    {
                        return Task.FromResult(existingResult);
                    }

                    // Token reused with differing payload: reject as conflict
                    var conflict = DeviceRegistrationResult.Failed(
                        "IDEMPOTENCY_CONFLICT",
                        $"Idempotency token '{request.IdempotencyToken}' has already been used with a differing registration payload.");
                    return Task.FromResult(conflict);
                }

                // 2. Collision check: ensure device code is not already registered to a different installation
                if (_registrationIdByCode.TryGetValue(request.DeviceCode, out var existingRegId))
                {
                    if (_devicesById.TryGetValue(existingRegId, out var existingRecord))
                    {
                        if (!string.Equals(existingRecord.InstallationId, request.InstallationId, StringComparison.OrdinalIgnoreCase))
                        {
                            var collision = DeviceRegistrationResult.Failed(
                                "DEVICE_CODE_COLLISION",
                                $"Device code '{request.DeviceCode}' is already registered to a different installation ({existingRecord.InstallationId}).");
                            _idempotencyResults[request.IdempotencyToken] = collision;
                            _idempotencySignatures[request.IdempotencyToken] = payloadSig;
                            return Task.FromResult(collision);
                        }
                    }
                }

                // 3. Create or update registration
                string registrationId = existingRegId ?? $"REG-{Guid.NewGuid():N}".ToUpperInvariant();
                string serverToken = $"SOPTKN-{Guid.NewGuid():N}{Guid.NewGuid():N}".ToUpperInvariant();

                var record = new DeviceRecord(
                    registrationId: registrationId,
                    deviceCode: request.DeviceCode,
                    installationId: request.InstallationId,
                    displayName: request.DisplayName,
                    status: DeviceStatus.Active,
                    ownership: request.Ownership,
                    metadata: request.Metadata,
                    fingerprintCombinedHash: request.Fingerprint?.CombinedHash,
                    fingerprintAlgorithm: request.Fingerprint?.Algorithm.ToString(),
                    registeredAtUtc: DateTimeOffset.UtcNow,
                    lastHeartbeatUtc: DateTimeOffset.UtcNow,
                    lastSyncAtUtc: DateTimeOffset.UtcNow,
                    updatedAtUtc: DateTimeOffset.UtcNow,
                    version: 1);

                _devicesById[registrationId] = record;
                _registrationIdByCode[request.DeviceCode] = registrationId;

                // 4. Log audit event
                RecordAudit(DeviceAuditEvent.Create(
                    registrationId,
                    request.DeviceCode,
                    "Registered",
                    request.Ownership.AssignedToUserId ?? "System",
                    $"Device '{record.DisplayName}' registered successfully for tenant '{request.Ownership.TenantId}'."));

                var result = DeviceRegistrationResult.Succeeded(record, serverToken);
                _idempotencyResults[request.IdempotencyToken] = result;
                _idempotencySignatures[request.IdempotencyToken] = payloadSig;

                return Task.FromResult(result);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord?> GetAsync(string registrationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(registrationId)) return Task.FromResult<DeviceRecord?>(null);

            lock (_syncLock)
            {
                _devicesById.TryGetValue(registrationId.Trim(), out var record);
                return Task.FromResult<DeviceRecord?>(record);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord?> GetByDeviceCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deviceCode)) return Task.FromResult<DeviceRecord?>(null);

            lock (_syncLock)
            {
                if (_registrationIdByCode.TryGetValue(deviceCode.Trim(), out var regId))
                {
                    _devicesById.TryGetValue(regId, out var record);
                    return Task.FromResult<DeviceRecord?>(record);
                }
                return Task.FromResult<DeviceRecord?>(null);
            }
        }

        /// <inheritdoc />
        public Task<DeviceHeartbeatResult> ReportHeartbeatAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default)
        {
            if (heartbeat == null) throw new ArgumentNullException(nameof(heartbeat));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(heartbeat.RegistrationId, out var record))
                {
                    return Task.FromResult(new DeviceHeartbeatResult(
                        acknowledged: false,
                        status: DeviceStatus.Unregistered,
                        serverTimestampUtc: DateTimeOffset.UtcNow,
                        requiresSync: false,
                        instructions: "Device registration ID not recognized. Re-registration required."));
                }

                // If device is Revoked or Suspended, instruct client to stop privileged operations
                if (record.Status == DeviceStatus.Revoked)
                {
                    RecordAudit(DeviceAuditEvent.Create(
                        record.RegistrationId,
                        record.DeviceCode,
                        "HeartbeatRejected",
                        "Heartbeat",
                        "Heartbeat received from revoked device. Access denied."));

                    return Task.FromResult(DeviceHeartbeatResult.Revoked(record.StatusReason ?? "Device registration has been permanently revoked."));
                }

                if (record.Status == DeviceStatus.Suspended)
                {
                    RecordAudit(DeviceAuditEvent.Create(
                        record.RegistrationId,
                        record.DeviceCode,
                        "HeartbeatSuspended",
                        "Heartbeat",
                        "Heartbeat received from suspended device. Operations held."));

                    return Task.FromResult(DeviceHeartbeatResult.Suspended(record.StatusReason ?? "Device registration is temporarily suspended."));
                }

                // Active device: update heartbeat and metadata
                var updated = record.WithHeartbeat(heartbeat.TimestampUtc, heartbeat.AppVersion);
                _devicesById[record.RegistrationId] = updated;

                RecordAudit(DeviceAuditEvent.Create(
                    record.RegistrationId,
                    record.DeviceCode,
                    "HeartbeatReceived",
                    "Heartbeat",
                    $"Heartbeat sequence #{heartbeat.SequenceNumber} received. Status: {heartbeat.StatusSummary}."));

                return Task.FromResult(DeviceHeartbeatResult.Active(TimeSpan.FromDays(14), requiresSync: false));
            }
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceRecord>> ListDevicesAsync(
            string tenantId,
            DeviceStatus? statusFilter = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return Task.FromResult<IReadOnlyList<DeviceRecord>>(Array.Empty<DeviceRecord>());

            lock (_syncLock)
            {
                var query = _devicesById.Values
                    .Where(d => string.Equals(d.Ownership.TenantId, tenantId.Trim(), StringComparison.OrdinalIgnoreCase));

                if (statusFilter.HasValue)
                {
                    query = query.Where(d => d.Status == statusFilter.Value);
                }

                return Task.FromResult<IReadOnlyList<DeviceRecord>>(query.OrderBy(d => d.DisplayName).ToList());
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord?> GetDeviceAsync(string registrationId, CancellationToken cancellationToken = default)
        {
            return GetAsync(registrationId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<DeviceRecord> RenameDeviceAsync(
            string registrationId,
            string newDisplayName,
            string operatorId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(registrationId)) throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            if (string.IsNullOrWhiteSpace(newDisplayName)) throw new ArgumentException("NewDisplayName is required.", nameof(newDisplayName));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(registrationId.Trim(), out var record))
                    throw new KeyNotFoundException($"Device registration '{registrationId}' was not found.");

                var updated = record.WithDisplayName(newDisplayName);
                _devicesById[registrationId.Trim()] = updated;

                RecordAudit(DeviceAuditEvent.Create(
                    record.RegistrationId,
                    record.DeviceCode,
                    "Renamed",
                    operatorId ?? "Admin",
                    $"Renamed from '{record.DisplayName}' to '{newDisplayName}'."));

                return Task.FromResult(updated);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord> ReassignDeviceAsync(
            string registrationId,
            DeviceOwnership newOwnership,
            string operatorId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(registrationId)) throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            if (newOwnership == null) throw new ArgumentNullException(nameof(newOwnership));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(registrationId.Trim(), out var record))
                    throw new KeyNotFoundException($"Device registration '{registrationId}' was not found.");

                var updated = record.WithOwnership(newOwnership);
                _devicesById[registrationId.Trim()] = updated;

                RecordAudit(DeviceAuditEvent.Create(
                    record.RegistrationId,
                    record.DeviceCode,
                    "Reassigned",
                    operatorId ?? "Admin",
                    $"Reassigned to Tenant: {newOwnership.TenantId}, Branch: {newOwnership.BranchId}, User: {newOwnership.AssignedToUserId}."));

                return Task.FromResult(updated);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord> SuspendDeviceAsync(
            string registrationId,
            string reason,
            string operatorId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(registrationId)) throw new ArgumentException("RegistrationId is required.", nameof(registrationId));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(registrationId.Trim(), out var record))
                    throw new KeyNotFoundException($"Device registration '{registrationId}' was not found.");

                var updated = record.WithStatus(DeviceStatus.Suspended, reason);
                _devicesById[registrationId.Trim()] = updated;

                RecordAudit(DeviceAuditEvent.Create(
                    record.RegistrationId,
                    record.DeviceCode,
                    "Suspended",
                    operatorId ?? "Admin",
                    $"Device suspended. Reason: {reason}"));

                return Task.FromResult(updated);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord> ReactivateDeviceAsync(
            string registrationId,
            string reason,
            string operatorId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(registrationId)) throw new ArgumentException("RegistrationId is required.", nameof(registrationId));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(registrationId.Trim(), out var record))
                    throw new KeyNotFoundException($"Device registration '{registrationId}' was not found.");

                if (record.Status == DeviceStatus.Revoked)
                    throw new InvalidOperationException($"Device '{registrationId}' has been permanently revoked and cannot be reactivated.");

                var updated = record.WithStatus(DeviceStatus.Active, reason);
                _devicesById[registrationId.Trim()] = updated;

                RecordAudit(DeviceAuditEvent.Create(
                    record.RegistrationId,
                    record.DeviceCode,
                    "Reactivated",
                    operatorId ?? "Admin",
                    $"Device reactivated. Reason: {reason}"));

                return Task.FromResult(updated);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord> RevokeDeviceAsync(
            DeviceRevocation revocation,
            CancellationToken cancellationToken = default)
        {
            if (revocation == null) throw new ArgumentNullException(nameof(revocation));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(revocation.RegistrationId.Trim(), out var record))
                    throw new KeyNotFoundException($"Device registration '{revocation.RegistrationId}' was not found.");

                var updated = record.WithStatus(DeviceStatus.Revoked, revocation.Reason);
                _devicesById[revocation.RegistrationId.Trim()] = updated;

                RecordAudit(DeviceAuditEvent.Create(
                    record.RegistrationId,
                    record.DeviceCode,
                    "Revoked",
                    revocation.RevokedByUserId ?? "Admin",
                    $"Device revoked ({revocation.RevocationType}). Reason: {revocation.Reason}"));

                return Task.FromResult(updated);
            }
        }

        /// <inheritdoc />
        public Task<DeviceRecord> ReplaceDeviceAsync(
            DeviceReplacementRequest replacement,
            CancellationToken cancellationToken = default)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));

            lock (_syncLock)
            {
                if (!_devicesById.TryGetValue(replacement.OldRegistrationId.Trim(), out var oldRecord))
                    throw new KeyNotFoundException($"Device registration '{replacement.OldRegistrationId}' was not found.");

                if (oldRecord.Status == DeviceStatus.Replaced)
                    throw new InvalidOperationException($"Device '{replacement.OldRegistrationId}' is already replaced.");

                string note = $"Replaced by new installation '{replacement.NewInstallationId}' (Code: {replacement.NewDeviceCode}). Reason: {replacement.Reason}";
                var updatedOld = oldRecord.WithStatus(DeviceStatus.Replaced, note);
                _devicesById[replacement.OldRegistrationId.Trim()] = updatedOld;

                RecordAudit(DeviceAuditEvent.Create(
                    oldRecord.RegistrationId,
                    oldRecord.DeviceCode,
                    "Replaced",
                    replacement.RequestedByUserId ?? "Admin",
                    note));

                return Task.FromResult(updatedOld);
            }
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceAuditEvent>> GetAuditEventsAsync(
            string registrationId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(registrationId))
                return Task.FromResult<IReadOnlyList<DeviceAuditEvent>>(Array.Empty<DeviceAuditEvent>());

            lock (_syncLock)
            {
                if (_auditEvents.TryGetValue(registrationId.Trim(), out var list))
                {
                    return Task.FromResult<IReadOnlyList<DeviceAuditEvent>>(list.ToList());
                }
                return Task.FromResult<IReadOnlyList<DeviceAuditEvent>>(Array.Empty<DeviceAuditEvent>());
            }
        }

        private void RecordAudit(DeviceAuditEvent ev)
        {
            if (!_auditEvents.TryGetValue(ev.RegistrationId, out var list))
            {
                list = new List<DeviceAuditEvent>();
                _auditEvents[ev.RegistrationId] = list;
            }
            list.Add(ev);
        }
    }
}
