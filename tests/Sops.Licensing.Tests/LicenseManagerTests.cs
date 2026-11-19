using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Activation;
using Sops.Licensing.Cryptography;
using Sops.Licensing.Extensions;
using Sops.Licensing.Models;
using Sops.Licensing.Storage.Windows;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class LicenseManagerTests
    {
        private readonly string _privateKeyXml;
        private readonly string _publicKeyXml;
        private readonly LicensingOptions _options;

        public LicenseManagerTests()
        {
            var (priv, pub, _) = CertificateSigner.GenerateRsaKeyPair();
            _privateKeyXml = priv;
            _publicKeyXml = pub;

            _options = new LicensingOptions
            {
                ProductId = "SOPs.Pharmacy",
                TrustedPublicKeys = new Dictionary<string, string>
                {
                    { "key-2026", _publicKeyXml }
                }
            };
        }

        private LicenseCertificate CreateSignedCertificate(params string[] entitlements)
        {
            var cert = new LicenseCertificate
            {
                LicenseId = "lic-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ProductId = "SOPs.Pharmacy",
                SubjectId = "TestPharmacyChain",
                PlanId = "ENTERPRISE",
                IssuedAtUtc = DateTimeOffset.UtcNow,
                ValidFromUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(365),
                OfflineValidUntilUtc = DateTimeOffset.UtcNow.AddDays(30),
                SigningKeyId = "key-2026",
                Entitlements = new List<string>(entitlements)
            };

            CertificateSigner.SignCertificate(cert, _privateKeyXml);
            return cert;
        }

        [Fact]
        public async Task LicenseManager_FullLifecycle_ActivateValidateRequireDeactivate()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_mgr_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "MgrTestNs");
                var manager = new LicenseManager(_options, storage);

                // Initially no license
                var initialValidation = await manager.ValidateAsync();
                Assert.False(initialValidation.IsValid);
                Assert.Equal(LicenseStatus.Missing, initialValidation.Status);

                // RequireAsync throws EntitlementDeniedException when missing license
                await Assert.ThrowsAsync<EntitlementDeniedException>(() => manager.RequireAsync("database.raw.read"));

                // Activate with signed certificate
                var cert = CreateSignedCertificate("database.connect", "database.raw.read", "pos.access");
                string certJson = JsonSerializer.Serialize(cert);

                var activationResult = await manager.ActivateAsync(certJson);
                Assert.True(activationResult.Success);
                Assert.NotNull(activationResult.Certificate);

                // Validate after activation
                var activeValidation = await manager.ValidateAsync();
                Assert.True(activeValidation.IsValid);
                Assert.Equal(LicenseStatus.Active, activeValidation.Status);

                // RequireAsync granted features
                await manager.RequireAsync("database.connect");
                await manager.RequireAsync("database.raw.read");
                await manager.RequireAsync("pos.access");

                // RequireAsync ungranted feature throws
                await Assert.ThrowsAsync<EntitlementDeniedException>(() => manager.RequireAsync("database.export"));

                // IsEntitledAsync checks
                Assert.True(await manager.IsEntitledAsync("database.raw.read"));
                Assert.False(await manager.IsEntitledAsync("database.export"));

                // GetActiveLicenseAsync
                var activeCert = await manager.GetActiveLicenseAsync();
                Assert.NotNull(activeCert);
                Assert.Equal(cert.LicenseId, activeCert!.LicenseId);

                // Deactivate
                bool deactivated = await manager.DeactivateAsync();
                Assert.True(deactivated);

                // Validate after deactivation
                var postDeactivation = await manager.ValidateAsync();
                Assert.False(postDeactivation.IsValid);
                Assert.Equal(LicenseStatus.Missing, postDeactivation.Status);
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
        public async Task LicenseManager_OfflineActivationWorkflow_RequestAndResponse()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_offline_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "OfflineTestNs");
                var manager = new LicenseManager(_options, storage);

                // 1. Create offline activation request
                string offlineRequestJson = await manager.CreateOfflineActivationRequestAsync("ACT-CODE-12345");
                Assert.NotEmpty(offlineRequestJson);
                Assert.Contains("activation_code", offlineRequestJson);
                Assert.Contains("device_fingerprint", offlineRequestJson);

                // 2. Simulate licensing authority creating signed response
                var cert = CreateSignedCertificate("app.login", "database.*");
                var response = new ActivationResponse
                {
                    Success = true,
                    CertificateRaw = JsonSerializer.Serialize(cert),
                    Message = "Offline activation response",
                    ServerTimestampUtc = DateTimeOffset.UtcNow
                };
                string responseJson = JsonSerializer.Serialize(response);

                // 3. Activate offline response
                var actResult = await manager.ActivateOfflineResponseAsync(responseJson);
                Assert.True(actResult.Success);

                // 4. Verify entitlements
                Assert.True(await manager.IsEntitledAsync("database.record.read"));
                Assert.True(await manager.IsEntitledAsync("database.record.update"));
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
        public void DependencyInjection_AddSopsLicensing_RegistersAllServices()
        {
            var services = new ServiceCollection();

            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Licensing:ProductId", "SOPs.Pharmacy" },
                { "Licensing:StorageNamespace", "DiTestNs" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemoryConfig)
                .Build();

            services.AddSopsLicensing(configuration);

            using var sp = services.BuildServiceProvider();

            var storage = sp.GetRequiredService<ISecureStorage>();
            Assert.NotNull(storage);

            var deviceId = sp.GetRequiredService<IDeviceIdentifier>();
            Assert.NotNull(deviceId);

            var evaluator = sp.GetRequiredService<IEntitlementEvaluator>();
            Assert.NotNull(evaluator);

            var validator = sp.GetRequiredService<ILicenseValidator>();
            Assert.NotNull(validator);

            var manager = sp.GetRequiredService<ILicenseManager>();
            Assert.NotNull(manager);
        }
    }
}
