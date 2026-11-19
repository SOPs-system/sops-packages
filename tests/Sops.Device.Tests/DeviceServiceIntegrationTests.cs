using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Extensions;
using Sops.Device.Models;
using Sops.Device.Persistence;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceServiceIntegrationTests
    {
        [Fact]
        public async Task EndToEnd_WithHybridMode_ProducesValidIdentityAndFingerprint()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDevice(options =>
            {
                options.CodePrefix = "SOP";
                options.IdentityMode = DeviceIdentityMode.Hybrid;
                options.AppVersion = "1.0.0";
                options.ApplicationSalt = "test-tenant-domain";
            });

            // Replace file store with in-memory store for test isolation
            services.AddSingleton<IDeviceIdentityStore, InMemoryDeviceIdentityStore>();

            using (var sp = services.BuildServiceProvider())
            {
                var service = sp.GetRequiredService<IDeviceService>();
                var codeGen = sp.GetRequiredService<IDeviceCodeGenerator>();

                // 1. GetDeviceCodeAsync
                string code = await service.GetDeviceCodeAsync();
                Assert.StartsWith("SOP-", code);
                Assert.True(codeGen.ValidateCode(code, out int version, out _));
                Assert.Equal(1, version);

                // 2. GetDeviceInfoAsync
                var info = await service.GetDeviceInfoAsync();
                Assert.NotNull(info);
                Assert.Equal(code, info.DeviceCode);
                Assert.False(string.IsNullOrWhiteSpace(info.InstallationId));

                // 3. GetIdentityAsync
                var identity = await service.GetIdentityAsync();
                Assert.NotNull(identity);
                Assert.Equal(info.DeviceCode, identity.DeviceCode);
                Assert.Equal(info.InstallationId, identity.InstallationId);
                Assert.Equal(DeviceIdentityMode.Hybrid, identity.Mode);
                Assert.NotNull(identity.Fingerprint);
                Assert.Equal("test-tenant-domain", identity.Fingerprint!.Scope);

                // 4. GetFingerprintAsync
                var fp = await service.GetFingerprintAsync();
                Assert.NotNull(fp);
                Assert.Equal(identity.Fingerprint.CombinedHash, fp.CombinedHash);

                // 5. CompareAsync (Self-check should be exact match)
                var comparison = await service.CompareAsync(fp);
                Assert.NotNull(comparison);
                Assert.False(comparison.HasChanged);
                Assert.Equal(1.0, comparison.SimilarityScore);
                Assert.Equal(DeviceComparisonConfidence.ExactMatch, comparison.Confidence);
            }
        }

        [Fact]
        public async Task EndToEnd_WithInstallationMode_IgnoresHardwareFingerprint()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDevice(options =>
            {
                options.CodePrefix = "TEST";
                options.IdentityMode = DeviceIdentityMode.Installation;
            });
            services.AddSingleton<IDeviceIdentityStore, InMemoryDeviceIdentityStore>();

            using (var sp = services.BuildServiceProvider())
            {
                var service = sp.GetRequiredService<IDeviceService>();
                var identity = await service.GetIdentityAsync();

                Assert.Equal(DeviceIdentityMode.Installation, identity.Mode);
                Assert.StartsWith("TEST-", identity.DeviceCode);
                Assert.Null(identity.Fingerprint);
            }
        }

        [Fact]
        public async Task EndToEnd_PersistenceReload_RetainsExistingDeviceCode()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_device_e2e_" + Guid.NewGuid().ToString("N"));
            string storePath = Path.Combine(tempDir, "identity.json");

            try
            {
                string firstDeviceCode;
                string firstInstallationId;

                // First boot
                var services1 = new ServiceCollection();
                services1.AddLogging();
                services1.AddSopsDevice(options =>
                {
                    options.StoragePath = storePath;
                });

                using (var sp1 = services1.BuildServiceProvider())
                {
                    var service1 = sp1.GetRequiredService<IDeviceService>();
                    var id1 = await service1.GetIdentityAsync();
                    firstDeviceCode = id1.DeviceCode;
                    firstInstallationId = id1.InstallationId;
                }

                // Second boot (reloaded from disk)
                var services2 = new ServiceCollection();
                services2.AddLogging();
                services2.AddSopsDevice(options =>
                {
                    options.StoragePath = storePath;
                });

                using (var sp2 = services2.BuildServiceProvider())
                {
                    var service2 = sp2.GetRequiredService<IDeviceService>();
                    var id2 = await service2.GetIdentityAsync();

                    Assert.Equal(firstDeviceCode, id2.DeviceCode);
                    Assert.Equal(firstInstallationId, id2.InstallationId);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task EndToEnd_WithNativeSignalResolver_InjectsMobileSignalsGracefully()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDevice(options =>
            {
                options.CustomPlatform = DevicePlatform.Android;
                options.NativeSignalResolver = key =>
                {
                    if (key == "AndroidId") return "mock-android-id-12345678";
                    if (key == "Manufacturer") return "Samsung";
                    if (key == "Model") return "GalaxyS24";
                    return null;
                };
            });
            services.AddSingleton<IDeviceIdentityStore, InMemoryDeviceIdentityStore>();

            using (var sp = services.BuildServiceProvider())
            {
                var service = sp.GetRequiredService<IDeviceService>();
                var fp = await service.GetFingerprintAsync();

                Assert.Contains("AndroidScopedId", fp.IncludedSignals);
                Assert.Contains("AndroidDeviceModel", fp.IncludedSignals);
            }
        }
    }
}
