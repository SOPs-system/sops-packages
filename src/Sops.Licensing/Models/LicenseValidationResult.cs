using System;
using System.Collections.Generic;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Status categories representing license validation state.
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>License is valid, cryptographically verified, and fully active.</summary>
        Active,

        /// <summary>No license certificate has been installed or activated.</summary>
        Missing,

        /// <summary>Cryptographic digital signature verification failed (tampered or wrong key).</summary>
        InvalidSignature,

        /// <summary>License has passed its absolute expiration date.</summary>
        Expired,

        /// <summary>License has not yet reached its effective start date.</summary>
        NotYetValid,

        /// <summary>Offline grace period has lapsed; online connection is required to refresh validity.</summary>
        OfflinePeriodExpired,

        /// <summary>License is bound to a different machine/hardware fingerprint.</summary>
        DeviceMismatch,

        /// <summary>License was explicitly revoked by the issuer.</summary>
        Revoked,

        /// <summary>Clock rollback detected (system time is prior to previously verified trusted timestamp).</summary>
        ClockRollbackDetected,

        /// <summary>Certificate is malformed, corrupt, or has an unsupported format version.</summary>
        Corrupted
    }

    /// <summary>
    /// Comprehensive outcome of license certificate and environment validation.
    /// </summary>
    public class LicenseValidationResult
    {
        /// <summary>Gets whether the license is verified and currently authorized to run.</summary>
        public bool IsValid => Status == LicenseStatus.Active;

        /// <summary>Gets the detailed validation status category.</summary>
        public LicenseStatus Status { get; set; } = LicenseStatus.Missing;

        /// <summary>Gets a human-readable diagnostic message explaining the validation outcome.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets the verified <see cref="LicenseCertificate"/>, or <c>null</c> if validation failed.</summary>
        public LicenseCertificate? Certificate { get; set; }

        /// <summary>Gets the UTC timestamp when the validation was evaluated.</summary>
        public DateTimeOffset ValidatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Gets the number of days remaining until expiration, or 0 if expired.</summary>
        public int DaysRemaining
        {
            get
            {
                if (Certificate == null) return 0;
                var remaining = Certificate.ExpiresAtUtc - ValidatedAtUtc;
                return remaining.TotalDays > 0 ? (int)Math.Ceiling(remaining.TotalDays) : 0;
            }
        }

        /// <summary>Gets the number of days remaining in the offline grace period.</summary>
        public int OfflineDaysRemaining
        {
            get
            {
                if (Certificate == null) return 0;
                var remaining = Certificate.OfflineValidUntilUtc - ValidatedAtUtc;
                return remaining.TotalDays > 0 ? (int)Math.Ceiling(remaining.TotalDays) : 0;
            }
        }

        /// <summary>Gets non-fatal warnings encountered during validation (e.g. impending expiration, offline grace period warning).</summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>Creates a successful active validation result.</summary>
        public static LicenseValidationResult Success(LicenseCertificate certificate, string message = "License is valid and active.")
        {
            return new LicenseValidationResult
            {
                Status = LicenseStatus.Active,
                Certificate = certificate,
                Message = message
            };
        }

        /// <summary>Creates a failed validation result with a specific status and diagnostic message.</summary>
        public static LicenseValidationResult Failure(LicenseStatus status, string message, LicenseCertificate? certificate = null)
        {
            return new LicenseValidationResult
            {
                Status = status,
                Message = message,
                Certificate = certificate
            };
        }
    }
}
