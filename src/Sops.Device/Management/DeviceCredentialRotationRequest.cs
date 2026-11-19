using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Request dispatched to rotate an existing device credential.
    /// Provides cryptographic continuity during periodic rotation or following suspicious activity.
    /// </summary>
    public sealed class DeviceCredentialRotationRequest
    {
        /// <summary>Authoritative server registration identifier.</summary>
        public string RegistrationId { get; }

        /// <summary>Current active device token proving possession of existing credentials.</summary>
        public string CurrentDeviceToken { get; }

        /// <summary>Client-generated idempotency token for the rotation attempt.</summary>
        public string IdempotencyToken { get; }

        /// <summary>Operational justification for rotation (e.g. "PeriodicScheduledRotation", "CompromiseSuspected").</summary>
        public string Reason { get; }

        /// <summary>Timestamp in UTC when rotation was requested.</summary>
        public DateTimeOffset TimestampUtc { get; }

        public DeviceCredentialRotationRequest(
            string registrationId,
            string currentDeviceToken,
            string idempotencyToken,
            string reason,
            DateTimeOffset? timestampUtc = null)
        {
            RegistrationId = !string.IsNullOrWhiteSpace(registrationId)
                ? registrationId.Trim()
                : throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            CurrentDeviceToken = !string.IsNullOrWhiteSpace(currentDeviceToken)
                ? currentDeviceToken.Trim()
                : throw new ArgumentException("CurrentDeviceToken is required.", nameof(currentDeviceToken));
            IdempotencyToken = !string.IsNullOrWhiteSpace(idempotencyToken)
                ? idempotencyToken.Trim()
                : throw new ArgumentException("IdempotencyToken is required.", nameof(idempotencyToken));
            Reason = !string.IsNullOrWhiteSpace(reason)
                ? reason.Trim()
                : throw new ArgumentException("Reason is required.", nameof(reason));
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
        }
    }
}
