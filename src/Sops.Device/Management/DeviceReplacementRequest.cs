using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Request to replace an existing registered device record with a new physical/software installation.
    /// Used during authorized hardware migrations and workstation replacements.
    /// </summary>
    public sealed class DeviceReplacementRequest
    {
        /// <summary>Existing registration identifier of the device being retired/replaced.</summary>
        public string OldRegistrationId { get; }

        /// <summary>Installation GUID of the newly deployed device.</summary>
        public string NewInstallationId { get; }

        /// <summary>Crockford Base32 device code of the newly deployed device.</summary>
        public string NewDeviceCode { get; }

        /// <summary>Business justification or technical explanation for replacement.</summary>
        public string Reason { get; }

        /// <summary>Administrative or technician operator ID authorizing the migration.</summary>
        public string RequestedByUserId { get; }

        /// <summary>Timestamp in UTC when replacement was requested.</summary>
        public DateTimeOffset ReplacementTimestampUtc { get; }

        public DeviceReplacementRequest(
            string oldRegistrationId,
            string newInstallationId,
            string newDeviceCode,
            string reason,
            string requestedByUserId,
            DateTimeOffset? replacementTimestampUtc = null)
        {
            OldRegistrationId = !string.IsNullOrWhiteSpace(oldRegistrationId)
                ? oldRegistrationId.Trim()
                : throw new ArgumentException("OldRegistrationId is required.", nameof(oldRegistrationId));
            NewInstallationId = !string.IsNullOrWhiteSpace(newInstallationId)
                ? newInstallationId.Trim()
                : throw new ArgumentException("NewInstallationId is required.", nameof(newInstallationId));
            NewDeviceCode = !string.IsNullOrWhiteSpace(newDeviceCode)
                ? newDeviceCode.Trim()
                : throw new ArgumentException("NewDeviceCode is required.", nameof(newDeviceCode));
            Reason = !string.IsNullOrWhiteSpace(reason)
                ? reason.Trim()
                : throw new ArgumentException("Reason is required.", nameof(reason));
            RequestedByUserId = !string.IsNullOrWhiteSpace(requestedByUserId)
                ? requestedByUserId.Trim()
                : throw new ArgumentException("RequestedByUserId is required.", nameof(requestedByUserId));
            ReplacementTimestampUtc = replacementTimestampUtc ?? DateTimeOffset.UtcNow;
        }
    }
}
