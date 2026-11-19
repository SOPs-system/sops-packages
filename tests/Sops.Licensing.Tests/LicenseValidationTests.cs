using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Sops.Licensing.Cryptography;
using Sops.Licensing.Device;
using Sops.Licensing.Models;
using Sops.Licensing.Storage.Windows;
using Sops.Licensing.Validation;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class LicenseValidationTests
    {
        private readonly string _privateKeyXml;
        private readonly string _publicKeyXml;
        private readonly LicensingOptions _options;
        private readonly DeviceIdentifier _deviceIdentifier;

        public LicenseValidationTests()
        {
            var (priv, pub, _) = CertificateSigner.GenerateRsaKeyPair();
            _privateKeyXml = priv;
            _publicKeyXml = pub;

            _options = new LicensingOptions
            {
                ProductId = "SOPs.Pharmacy",
                TrustedPublicKeys = new Dictionary<string, string>
                {
                    { "test-key-01", _publicKeyXml }
                },
                ClockRollbackToleranceSeconds = 60
            };

            _deviceIdentifier = new DeviceIdentifier();
        }

        private LicenseCertificate CreateSignedCertificate(
            DateTimeOffset validFrom,
            DateTimeOffset expiresAt,
            DateTimeOffset offlineUntil,
            string? deviceBinding = null)
        {
            var cert = new LicenseCertificate
            {
                LicenseId = "lic-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ProductId = "SOPs.Pharmacy",
                SubjectId = "TestPharmacyChain",
                PlanId = "PREMIUM",
                IssuedAtUtc = DateTimeOffset.UtcNow,
                ValidFromUtc = validFrom,
                ExpiresAtUtc = expiresAt,
                OfflineValidUntilUtc = offlineUntil,
                DeviceBinding = deviceBinding,
                SigningKeyId = "test-key-01",
                Entitlements = new List<string> { "database.*", "pos.*" }
            };

            CertificateSigner.SignCertificate(cert, _privateKeyXml);
            return cert;
        }

        [Fact]
        public async Task ValidateAsync_ValidActiveCertificate_ReturnsSuccess()
        {
            var validator = new LicenseValidator(_options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                DateTimeOffset.UtcNow.AddDays(14));

            var result = await validator.ValidateAsync(cert);

            Assert.True(result.IsValid);
            Assert.Equal(LicenseStatus.Active, result.Status);
            Assert.Equal(30, result.DaysRemaining);
            Assert.Equal(14, result.OfflineDaysRemaining);
        }

        [Fact]
        public async Task ValidateAsync_ExpiredCertificate_ReturnsExpiredStatus()
        {
            var validator = new LicenseValidator(_options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow.AddDays(-1), // Expired
                DateTimeOffset.UtcNow.AddDays(14));

            var result = await validator.ValidateAsync(cert);

            Assert.False(result.IsValid);
            Assert.Equal(LicenseStatus.Expired, result.Status);
            Assert.Equal(0, result.DaysRemaining);
        }

        [Fact]
        public async Task ValidateAsync_NotYetValidCertificate_ReturnsNotYetValidStatus()
        {
            var validator = new LicenseValidator(_options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(5), // Future
                DateTimeOffset.UtcNow.AddDays(35),
                DateTimeOffset.UtcNow.AddDays(14));

            var result = await validator.ValidateAsync(cert);

            Assert.False(result.IsValid);
            Assert.Equal(LicenseStatus.NotYetValid, result.Status);
        }

        [Fact]
        public async Task ValidateAsync_OfflinePeriodExpired_ReturnsOfflinePeriodExpired()
        {
            var validator = new LicenseValidator(_options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-10),
                DateTimeOffset.UtcNow.AddDays(300), // Expiration is next year
                DateTimeOffset.UtcNow.AddDays(-1)); // Offline validity expired

            var result = await validator.ValidateAsync(cert);

            Assert.False(result.IsValid);
            Assert.Equal(LicenseStatus.OfflinePeriodExpired, result.Status);
        }

        [Fact]
        public async Task ValidateAsync_DeviceMismatch_ReturnsDeviceMismatch()
        {
            var validator = new LicenseValidator(_options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                DateTimeOffset.UtcNow.AddDays(14),
                deviceBinding: "foreign_device_fingerprint_hash_123");

            var result = await validator.ValidateAsync(cert);

            Assert.False(result.IsValid);
            Assert.Equal(LicenseStatus.DeviceMismatch, result.Status);
        }

        [Fact]
        public async Task ValidateAsync_RevokedLicense_ReturnsRevokedStatus()
        {
            var validator = new LicenseValidator(_options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                DateTimeOffset.UtcNow.AddDays(14));

            validator.AddRevocation(cert.LicenseId);

            var result = await validator.ValidateAsync(cert);

            Assert.False(result.IsValid);
            Assert.Equal(LicenseStatus.Revoked, result.Status);
        }

        [Fact]
        public async Task ValidateAsync_ClockRollbackDetected_ReturnsClockRollbackDetected()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_clock_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "ClockTestNs");
                var validator = new LicenseValidator(_options, _deviceIdentifier, storage);

                var cert = CreateSignedCertificate(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(30),
                    DateTimeOffset.UtcNow.AddDays(14));

                // 1. Advance clock to today
                var res1 = await validator.ValidateAsync(cert);
                Assert.True(res1.IsValid);

                // 2. Advance clock to future (e.g. 5 days from now)
                var futureTracker = new TrustedClockTracker(storage);
                await futureTracker.ValidateAndAdvanceClockAsync(DateTimeOffset.UtcNow.AddDays(5));

                // 3. Now validate again with current time (which is 5 days earlier than trusted time)
                var res2 = await validator.ValidateAsync(cert);
                Assert.False(res2.IsValid);
                Assert.Equal(LicenseStatus.ClockRollbackDetected, res2.Status);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task ValidateAsync_RevokedDeviceRegistration_FailsEvenIfHardwareMatches()
        {
            var fp = _deviceIdentifier.GetCurrentFingerprint();
            var options = new LicensingOptions
            {
                ProductId = "SOPs.Pharmacy",
                TrustedPublicKeys = new Dictionary<string, string>
                {
                    { "test-key-01", _publicKeyXml }
                },
                DeviceStatusProvider = () => Sops.Device.Management.DeviceStatus.Revoked
            };

            var validator = new LicenseValidator(options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                DateTimeOffset.UtcNow.AddDays(14),
                deviceBinding: fp.CombinedHash);

            var result = await validator.ValidateAsync(cert);

            Assert.False(result.IsValid);
            Assert.Equal(LicenseStatus.DeviceMismatch, result.Status);
            Assert.Contains("revoked", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateAsync_ActiveDeviceRegistration_Succeeds()
        {
            var fp = _deviceIdentifier.GetCurrentFingerprint();
            var options = new LicensingOptions
            {
                ProductId = "SOPs.Pharmacy",
                TrustedPublicKeys = new Dictionary<string, string>
                {
                    { "test-key-01", _publicKeyXml }
                },
                DeviceStatusProvider = () => Sops.Device.Management.DeviceStatus.Active
            };

            var validator = new LicenseValidator(options, _deviceIdentifier);
            var cert = CreateSignedCertificate(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                DateTimeOffset.UtcNow.AddDays(14),
                deviceBinding: fp.CombinedHash);

            var result = await validator.ValidateAsync(cert);

            Assert.True(result.IsValid);
            Assert.Equal(LicenseStatus.Active, result.Status);
        }
    }
}
