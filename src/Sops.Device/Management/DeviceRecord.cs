using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Core authoritative record of a registered device maintained by the device registry.
    /// Tracks ownership, lifecycle state, hardware anchor summary, and synchronization history.
    /// </summary>
    public sealed class DeviceRecord
    {
        /// <summary>Unique authoritative server registration ID.</summary>
        public string RegistrationId { get; }

        /// <summary>Human-readable, checksummed Crockford Base32 device code (e.g. SOP-WIN-XXXX-XXXX-C).</summary>
        public string DeviceCode { get; }

        /// <summary>Software installation GUID.</summary>
        public string InstallationId { get; }

        /// <summary>User-friendly display name (e.g. "Counter POS 1 - Main Branch").</summary>
        public string DisplayName { get; }

        /// <summary>Authoritative registration status.</summary>
        public DeviceStatus Status { get; }

        /// <summary>Pharmacy tenant and organizational ownership boundary.</summary>
        public DeviceOwnership Ownership { get; }

        /// <summary>Operating system and device metadata.</summary>
        public DeviceMetadata Metadata { get; }

        /// <summary>Combined hash of the hardware fingerprint recorded at registration.</summary>
        public string? FingerprintCombinedHash { get; }

        /// <summary>Fingerprint algorithm name (e.g. "HMAC-SHA256" or "SHA-256").</summary>
        public string? FingerprintAlgorithm { get; }

        /// <summary>Timestamp in UTC when initial registration occurred.</summary>
        public DateTimeOffset RegisteredAtUtc { get; }

        /// <summary>Timestamp in UTC of the most recent heartbeat ping.</summary>
        public DateTimeOffset? LastHeartbeatUtc { get; }

        /// <summary>Timestamp in UTC of the most recent bidirectional sync.</summary>
        public DateTimeOffset? LastSyncAtUtc { get; }

        /// <summary>Timestamp in UTC when this record was last modified.</summary>
        public DateTimeOffset UpdatedAtUtc { get; }

        /// <summary>Diagnostic or administrative note explaining current status (e.g. reason for suspension).</summary>
        public string? StatusReason { get; }

        /// <summary>Optimistic concurrency version tag.</summary>
        public long Version { get; }

        public DeviceRecord(
            string registrationId,
            string deviceCode,
            string installationId,
            string displayName,
            DeviceStatus status,
            DeviceOwnership ownership,
            DeviceMetadata metadata,
            string? fingerprintCombinedHash = null,
            string? fingerprintAlgorithm = null,
            DateTimeOffset? registeredAtUtc = null,
            DateTimeOffset? lastHeartbeatUtc = null,
            DateTimeOffset? lastSyncAtUtc = null,
            DateTimeOffset? updatedAtUtc = null,
            string? statusReason = null,
            long version = 1)
        {
            RegistrationId = !string.IsNullOrWhiteSpace(registrationId)
                ? registrationId.Trim()
                : throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            DeviceCode = !string.IsNullOrWhiteSpace(deviceCode)
                ? deviceCode.Trim()
                : throw new ArgumentException("DeviceCode is required.", nameof(deviceCode));
            InstallationId = !string.IsNullOrWhiteSpace(installationId)
                ? installationId.Trim()
                : throw new ArgumentException("InstallationId is required.", nameof(installationId));
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : deviceCode;
            Status = status;
            Ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            Metadata = metadata ?? new DeviceMetadata();
            FingerprintCombinedHash = fingerprintCombinedHash;
            FingerprintAlgorithm = fingerprintAlgorithm;
            RegisteredAtUtc = registeredAtUtc ?? DateTimeOffset.UtcNow;
            LastHeartbeatUtc = lastHeartbeatUtc;
            LastSyncAtUtc = lastSyncAtUtc;
            UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
            StatusReason = statusReason;
            Version = version;
        }

        public DeviceRecord WithStatus(DeviceStatus newStatus, string? reason = null)
        {
            return new DeviceRecord(
                RegistrationId,
                DeviceCode,
                InstallationId,
                DisplayName,
                newStatus,
                Ownership,
                Metadata,
                FingerprintCombinedHash,
                FingerprintAlgorithm,
                RegisteredAtUtc,
                LastHeartbeatUtc,
                LastSyncAtUtc,
                DateTimeOffset.UtcNow,
                reason ?? StatusReason,
                Version + 1);
        }

        public DeviceRecord WithHeartbeat(DateTimeOffset timestampUtc, string? appVersion = null)
        {
            var updatedMetadata = appVersion != null
                ? new DeviceMetadata(
                    Metadata.Hostname,
                    Metadata.OsPlatform,
                    Metadata.OsVersion,
                    appVersion,
                    Metadata.Architecture,
                    Metadata.DeviceModel,
                    Metadata.TotalMemoryMb,
                    Metadata.CustomAttributes)
                : Metadata;

            return new DeviceRecord(
                RegistrationId,
                DeviceCode,
                InstallationId,
                DisplayName,
                Status,
                Ownership,
                updatedMetadata,
                FingerprintCombinedHash,
                FingerprintAlgorithm,
                RegisteredAtUtc,
                timestampUtc,
                LastSyncAtUtc,
                DateTimeOffset.UtcNow,
                StatusReason,
                Version + 1);
        }

        public DeviceRecord WithSync(DateTimeOffset syncTimestampUtc)
        {
            return new DeviceRecord(
                RegistrationId,
                DeviceCode,
                InstallationId,
                DisplayName,
                Status,
                Ownership,
                Metadata,
                FingerprintCombinedHash,
                FingerprintAlgorithm,
                RegisteredAtUtc,
                LastHeartbeatUtc,
                syncTimestampUtc,
                DateTimeOffset.UtcNow,
                StatusReason,
                Version + 1);
        }

        public DeviceRecord WithDisplayName(string newDisplayName)
        {
            return new DeviceRecord(
                RegistrationId,
                DeviceCode,
                InstallationId,
                newDisplayName,
                Status,
                Ownership,
                Metadata,
                FingerprintCombinedHash,
                FingerprintAlgorithm,
                RegisteredAtUtc,
                LastHeartbeatUtc,
                LastSyncAtUtc,
                DateTimeOffset.UtcNow,
                StatusReason,
                Version + 1);
        }

        public DeviceRecord WithOwnership(DeviceOwnership newOwnership)
        {
            return new DeviceRecord(
                RegistrationId,
                DeviceCode,
                InstallationId,
                DisplayName,
                Status,
                newOwnership,
                Metadata,
                FingerprintCombinedHash,
                FingerprintAlgorithm,
                RegisteredAtUtc,
                LastHeartbeatUtc,
                LastSyncAtUtc,
                DateTimeOffset.UtcNow,
                StatusReason,
                Version + 1);
        }
    }
}
