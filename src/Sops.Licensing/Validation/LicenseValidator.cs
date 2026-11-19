using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Cryptography;
using Sops.Licensing.Device;
using Sops.Licensing.Models;

namespace Sops.Licensing.Validation
{
    /// <summary>
    /// Default implementation of <see cref="ILicenseValidator"/> performing cryptographic signature checks,
    /// expiration verification, device binding verification, clock rollback detection, and entitlement validation.
    /// </summary>
    public class LicenseValidator : ILicenseValidator
    {
        private readonly LicensingOptions _options;
        private readonly IDeviceIdentifier _deviceIdentifier;
        private readonly TrustedClockTracker _clockTracker;
        private readonly ILogger<LicenseValidator> _logger;

        private readonly HashSet<string> _localRevocationList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="LicenseValidator"/> class.
        /// </summary>
        public LicenseValidator(
            LicensingOptions options,
            IDeviceIdentifier? deviceIdentifier = null,
            ISecureStorage? secureStorage = null,
            ILogger<LicenseValidator>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _deviceIdentifier = deviceIdentifier ?? new DeviceIdentifier(secureStorage);
            _clockTracker = new TrustedClockTracker(secureStorage, options.ClockRollbackToleranceSeconds);
            _logger = logger ?? NullLogger<LicenseValidator>.Instance;
        }

        /// <summary>
        /// Registers a license ID in the local revocation list.
        /// </summary>
        public void AddRevocation(string licenseId)
        {
            if (!string.IsNullOrWhiteSpace(licenseId))
            {
                lock (_localRevocationList)
                {
                    _localRevocationList.Add(licenseId.Trim());
                }
            }
        }

        /// <inheritdoc />
        public async Task<LicenseValidationResult> ValidateAsync(
            LicenseCertificate certificate,
            CancellationToken cancellationToken = default)
        {
            if (certificate == null)
            {
                return LicenseValidationResult.Failure(LicenseStatus.Missing, "No license certificate provided.");
            }

            // Step 1: Cryptographic Digital Signature Verification
            if (_options.TrustedPublicKeys.Count > 0)
            {
                bool isSignatureValid = CertificateVerifier.VerifySignature(certificate, _options.TrustedPublicKeys);
                if (!isSignatureValid)
                {
                    _logger.LogWarning("Digital signature verification failed for license ID: {LicenseId}", certificate.LicenseId);
                    return LicenseValidationResult.Failure(
                        LicenseStatus.InvalidSignature,
                        "License digital signature verification failed. Certificate may be forged, modified, or signed with an untrusted key.",
                        certificate);
                }
            }

            // Step 2: Product Identifier Verification
            if (!string.IsNullOrWhiteSpace(_options.ProductId) &&
                !string.IsNullOrWhiteSpace(certificate.ProductId) &&
                !string.Equals(_options.ProductId, certificate.ProductId, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.Failure(
                    LicenseStatus.Corrupted,
                    $"Product ID mismatch: license was issued for '{certificate.ProductId}', but current application is '{_options.ProductId}'.",
                    certificate);
            }

            // Step 3: Revocation Check
            lock (_localRevocationList)
            {
                if (_localRevocationList.Contains(certificate.LicenseId))
                {
                    return LicenseValidationResult.Failure(
                        LicenseStatus.Revoked,
                        $"License '{certificate.LicenseId}' has been revoked.",
                        certificate);
                }
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Step 4: Clock Rollback Detection
            bool clockValid = await _clockTracker.ValidateAndAdvanceClockAsync(now, cancellationToken).ConfigureAwait(false);
            if (!clockValid)
            {
                _logger.LogWarning("Clock rollback detected. System clock is earlier than previously verified trusted time.");
                return LicenseValidationResult.Failure(
                    LicenseStatus.ClockRollbackDetected,
                    "Suspicious clock rollback detected. System clock appears to have been set backwards.",
                    certificate);
            }

            // Step 5: Effective Date Verification
            if (certificate.ValidFromUtc > now)
            {
                return LicenseValidationResult.Failure(
                    LicenseStatus.NotYetValid,
                    $"License is not yet active. Effective start date is {certificate.ValidFromUtc:u}.",
                    certificate);
            }

            // Step 6: Expiration Date Verification
            if (certificate.ExpiresAtUtc <= now)
            {
                return LicenseValidationResult.Failure(
                    LicenseStatus.Expired,
                    $"License expired on {certificate.ExpiresAtUtc:u}.",
                    certificate);
            }

            // Step 7: Offline Grace Period Verification
            if (certificate.OfflineValidUntilUtc > DateTimeOffset.MinValue && certificate.OfflineValidUntilUtc <= now)
            {
                return LicenseValidationResult.Failure(
                    LicenseStatus.OfflinePeriodExpired,
                    $"Offline license period lapsed on {certificate.OfflineValidUntilUtc:u}. Online connection is required to refresh validity.",
                    certificate);
            }

            // Step 8: Device Registration and Binding Verification (Node-locked licenses)
            if (_options.DeviceStatusProvider != null)
            {
                var devStatus = _options.DeviceStatusProvider();
                if (devStatus == Sops.Device.Management.DeviceStatus.Revoked)
                {
                    _logger.LogWarning("Device registration is revoked for license ID: {LicenseId}", certificate.LicenseId);
                    return LicenseValidationResult.Failure(
                        LicenseStatus.DeviceMismatch,
                        "Device registration has been revoked. Workstation is not permitted to use this license.",
                        certificate);
                }
                if (devStatus == Sops.Device.Management.DeviceStatus.Suspended)
                {
                    _logger.LogWarning("Device registration is suspended for license ID: {LicenseId}", certificate.LicenseId);
                    return LicenseValidationResult.Failure(
                        LicenseStatus.DeviceMismatch,
                        "Device registration is temporarily suspended.",
                        certificate);
                }
            }

            if (!string.IsNullOrWhiteSpace(certificate.DeviceBinding))
            {
                bool deviceMatches = _deviceIdentifier.Matches(certificate.DeviceBinding!, _options.HardwareMatchTolerance);
                if (!deviceMatches)
                {
                    _logger.LogWarning("Device binding check failed for license ID: {LicenseId}", certificate.LicenseId);
                    return LicenseValidationResult.Failure(
                        LicenseStatus.DeviceMismatch,
                        "License is node-locked to a different machine or hardware configuration.",
                        certificate);
                }
            }

            // All checks passed
            var result = LicenseValidationResult.Success(certificate);

            // Warnings for impending expiration
            if (result.DaysRemaining <= 14)
            {
                result.Warnings.Add($"License will expire in {result.DaysRemaining} days.");
            }
            if (result.OfflineDaysRemaining > 0 && result.OfflineDaysRemaining <= 3)
            {
                result.Warnings.Add($"Offline validity period ends in {result.OfflineDaysRemaining} days. Connect online to refresh.");
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<LicenseValidationResult> ValidateRawCertificateAsync(
            string rawCertificateJson,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawCertificateJson))
            {
                return LicenseValidationResult.Failure(LicenseStatus.Missing, "Certificate JSON string is empty.");
            }

            try
            {
                var cert = JsonSerializer.Deserialize<LicenseCertificate>(rawCertificateJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (cert == null)
                {
                    return LicenseValidationResult.Failure(LicenseStatus.Corrupted, "Failed to parse license certificate JSON.");
                }

                return await ValidateAsync(cert, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return LicenseValidationResult.Failure(LicenseStatus.Corrupted, $"Certificate JSON parsing error: {ex.Message}");
            }
        }
    }
}
