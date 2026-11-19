using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sops.Device.Abstractions;
using Sops.Device.Extensions;
using Sops.Device.Persistence;
using Sops.Licensing.Device;
using Sops.Licensing.Storage.Windows;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class DeviceBindingTests
    {
        [Fact]
        public void DeviceIdentifier_GeneratesConsistentFingerprint()
        {
            var identifier = new DeviceIdentifier();
            var fp1 = identifier.GetCurrentFingerprint();
            var fp2 = identifier.GetCurrentFingerprint();

            Assert.NotEmpty(fp1.InstallationId);
            Assert.NotEmpty(fp1.CombinedHash);
            Assert.NotEmpty(fp1.Signals);

            // Installation ID and combined hash must be stable across runs
            Assert.Equal(fp1.InstallationId, fp2.InstallationId);
            Assert.Equal(fp1.CombinedHash, fp2.CombinedHash);

            // Verify individual signals are hashed hex strings
            foreach (var kvp in fp1.Signals)
            {
                Assert.NotEmpty(kvp.Key);
                Assert.NotEmpty(kvp.Value);
                Assert.Equal(64, kvp.Value.Length); // SHA-256 hex length
            }
        }

        [Fact]
        public void DeviceIdentifier_Matches_ExactHash_ReturnsTrue()
        {
            var identifier = new DeviceIdentifier();
            var fp = identifier.GetCurrentFingerprint();

            Assert.True(identifier.Matches(fp.CombinedHash));
            Assert.True(identifier.Matches(fp.InstallationId));
            Assert.False(identifier.Matches("foreign_device_hash_999"));
        }

        [Fact]
        public void DeviceIdentifier_FuzzyTolerance_SupportsComponentUpgrades()
        {
            var identifier = new DeviceIdentifier();
            var fp = identifier.GetCurrentFingerprint();

            // Simulate compound binding with 5 signals
            var signalsList = new System.Collections.Generic.List<string>(fp.Signals.Values);
            while (signalsList.Count < 5)
            {
                signalsList.Add(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
            }

            // Exactly matching all signals
            string fullBinding = fp.InstallationId + ":" + string.Join(";", signalsList);
            Assert.True(identifier.Matches(fullBinding, 0.7));

            // Simulate 1 component changed (e.g. 4 of 5 match = 80% > 70% threshold)
            signalsList[0] = "new_upgraded_hardware_component_hash_12345";
            string upgradedBinding = fp.InstallationId + ":" + string.Join(";", signalsList);
            Assert.True(identifier.Matches(upgradedBinding, 0.7));

            // Mismatched installation ID must always fail
            string foreignInstallIdBinding = Guid.NewGuid().ToString("D") + ":" + string.Join(";", signalsList);
            Assert.False(identifier.Matches(foreignInstallIdBinding, 0.7));
        }

        [Fact]
        public async Task DeviceIdentifier_WithSopsDeviceService_UsesSharedIdentityService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDevice(options =>
            {
                options.CodePrefix = "SOP";
            });
            services.AddSingleton<IDeviceIdentityStore, InMemoryDeviceIdentityStore>();

            using (var sp = services.BuildServiceProvider())
            {
                var deviceService = sp.GetRequiredService<IDeviceService>();
                var identifier = new DeviceIdentifier(deviceService);

                var fp = identifier.GetCurrentFingerprint();
                var identity = await deviceService.GetIdentityAsync();
                var sdkFp = await deviceService.GetFingerprintAsync();

                Assert.Equal(identity.InstallationId, fp.InstallationId);
                Assert.Equal(sdkFp.CombinedHash, fp.CombinedHash);
                Assert.True(identifier.Matches(sdkFp.CombinedHash));
                Assert.True(identifier.Matches(identity.InstallationId));
            }
        }

        [Fact]
        public void DeviceIdentifier_BackwardCompatibility_MatchesLegacyHashEvenWhenDeviceServiceIsActive()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDevice();
            services.AddSingleton<IDeviceIdentityStore, InMemoryDeviceIdentityStore>();

            using (var sp = services.BuildServiceProvider())
            {
                var deviceService = sp.GetRequiredService<IDeviceService>();

                // Compute legacy binding with standalone identifier
                var standaloneIdentifier = new DeviceIdentifier();
                var legacyFp = standaloneIdentifier.GetCurrentFingerprint();

                // Upgraded identifier using Sops.Device
                var upgradedIdentifier = new DeviceIdentifier(deviceService);

                // Both must recognize the existing legacy license binding!
                Assert.True(upgradedIdentifier.Matches(legacyFp.CombinedHash));
            }
        }
    }
}
