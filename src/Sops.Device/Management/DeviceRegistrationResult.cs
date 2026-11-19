using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Response returned by the authoritative device registry upon processing a device registration request.
    /// </summary>
    public sealed class DeviceRegistrationResult
    {
        /// <summary>Whether registration succeeded (status is Active or Pending).</summary>
        public bool Success { get; }

        /// <summary>Authoritative registration lifecycle status.</summary>
        public DeviceStatus Status { get; }

        /// <summary>Authoritative registration identifier assigned by the server.</summary>
        public string? RegistrationId { get; }

        /// <summary>Full authoritative device record, if successful.</summary>
        public DeviceRecord? Record { get; }

        /// <summary>
        /// Cryptographic server-issued device token or certificate reference used for subsequent client authentication.
        /// Distinct from the readable device code.
        /// </summary>
        public string? ServerToken => Credential?.DeviceToken ?? _serverToken;
        private readonly string? _serverToken;

        /// <summary>Server-issued credential details, including expiration and thumbprint.</summary>
        public DeviceCredential? Credential { get; }

        /// <summary>Machine-readable error code if registration failed.</summary>
        public string? ErrorCode { get; }

        /// <summary>Diagnostic error message if registration failed.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Timestamp in UTC when registration occurred.</summary>
        public DateTimeOffset? RegisteredAtUtc { get; }

        public DeviceRegistrationResult(
            bool success,
            DeviceStatus status,
            string? registrationId = null,
            DeviceRecord? record = null,
            string? serverToken = null,
            string? errorCode = null,
            string? errorMessage = null,
            DateTimeOffset? registeredAtUtc = null,
            DeviceCredential? credential = null)
        {
            Success = success;
            Status = status;
            RegistrationId = registrationId;
            Record = record;
            _serverToken = serverToken;
            Credential = credential;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            RegisteredAtUtc = registeredAtUtc;
        }

        public static DeviceRegistrationResult Succeeded(DeviceRecord record, DeviceCredential credential)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (credential == null) throw new ArgumentNullException(nameof(credential));
            return new DeviceRegistrationResult(
                success: true,
                status: record.Status,
                registrationId: record.RegistrationId,
                record: record,
                serverToken: credential.DeviceToken,
                registeredAtUtc: record.RegisteredAtUtc,
                credential: credential);
        }

        public static DeviceRegistrationResult Succeeded(DeviceRecord record, string serverToken)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var cred = new DeviceCredential(record.RegistrationId, serverToken, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
            return new DeviceRegistrationResult(
                success: true,
                status: record.Status,
                registrationId: record.RegistrationId,
                record: record,
                serverToken: serverToken,
                registeredAtUtc: record.RegisteredAtUtc,
                credential: cred);
        }

        public static DeviceRegistrationResult Failed(string errorCode, string errorMessage, DeviceStatus status = DeviceStatus.Unregistered)
        {
            return new DeviceRegistrationResult(
                success: false,
                status: status,
                errorCode: errorCode ?? "REGISTRATION_FAILED",
                errorMessage: errorMessage ?? "Device registration failed.");
        }
    }
}
