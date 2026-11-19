using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Explicit instruction to revoke a registered device, permanently disabling its credentials and access.
    /// </summary>
    public sealed class DeviceRevocation
    {
        /// <summary>Authoritative server registration identifier to revoke.</summary>
        public string RegistrationId { get; }

        /// <summary>Administrative or security reason for revocation.</summary>
        public string Reason { get; }

        /// <summary>Operator or security personnel who authorized revocation.</summary>
        public string RevokedByUserId { get; }

        /// <summary>Category of revocation: "Decommissioned", "Stolen", "Compromised", or "Administrative".</summary>
        public string RevocationType { get; }

        /// <summary>Timestamp in UTC when revocation was applied.</summary>
        public DateTimeOffset RevokedAtUtc { get; }

        public DeviceRevocation(
            string registrationId,
            string reason,
            string revokedByUserId,
            string revocationType = "Administrative",
            DateTimeOffset? revokedAtUtc = null)
        {
            RegistrationId = !string.IsNullOrWhiteSpace(registrationId)
                ? registrationId.Trim()
                : throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            Reason = !string.IsNullOrWhiteSpace(reason)
                ? reason.Trim()
                : throw new ArgumentException("Reason is required.", nameof(reason));
            RevokedByUserId = !string.IsNullOrWhiteSpace(revokedByUserId)
                ? revokedByUserId.Trim()
                : throw new ArgumentException("RevokedByUserId is required.", nameof(revokedByUserId));
            RevocationType = !string.IsNullOrWhiteSpace(revocationType) ? revocationType.Trim() : "Administrative";
            RevokedAtUtc = revokedAtUtc ?? DateTimeOffset.UtcNow;
        }
    }
}
