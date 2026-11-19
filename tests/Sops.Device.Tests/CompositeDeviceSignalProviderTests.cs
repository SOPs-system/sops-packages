using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Models;
using Sops.Device.Providers;
using Xunit;

namespace Sops.Device.Tests
{
    public class CompositeDeviceSignalProviderTests
    {
        [Fact]
        public async Task GetSignalsAsync_WithPlatformProvider_RoutesToPlatformProvider()
        {
            var platformDetector = Substitute.For<IDevicePlatformDetector>();
            platformDetector.Detect().Returns(DevicePlatform.Windows);

            var windowsProvider = Substitute.For<IDeviceSignalProvider>();
            windowsProvider.GetSignalsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DeviceSignal>>(new List<DeviceSignal>
            {
                DeviceSignal.Available("MachineGuid", "Registry", "val1", "hash1")
            }));

            var linuxProvider = Substitute.For<IDeviceSignalProvider>();
            var macProvider = Substitute.For<IDeviceSignalProvider>();
            var androidProvider = Substitute.For<IDeviceSignalProvider>();
            var iosProvider = Substitute.For<IDeviceSignalProvider>();
            var fallbackProvider = Substitute.For<IDeviceSignalProvider>();

            var options = new DeviceOptions();

            var composite = new CompositeDeviceSignalProvider(
                platformDetector,
                options,
                windowsProvider,
                linuxProvider,
                macProvider,
                androidProvider,
                iosProvider,
                fallbackProvider,
                NullLogger<CompositeDeviceSignalProvider>.Instance);

            var signals = await composite.GetSignalsAsync();

            Assert.NotNull(signals);
            Assert.Contains(signals, s => s.Name == "MachineGuid" && s.IsAvailable);
            Assert.DoesNotContain(signals, s => s.Name == "InstallationId"); // Hardware signals are strictly physical/OS anchors
        }

        [Fact]
        public async Task GetSignalsAsync_WhenNoHardwareSignals_FallsBackToFallbackProvider()
        {
            var platformDetector = Substitute.For<IDevicePlatformDetector>();
            platformDetector.Detect().Returns(DevicePlatform.Linux);

            // Linux provider returns empty signals
            var linuxProvider = Substitute.For<IDeviceSignalProvider>();
            linuxProvider.GetSignalsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DeviceSignal>>(new List<DeviceSignal>()));

            var windowsProvider = Substitute.For<IDeviceSignalProvider>();
            var macProvider = Substitute.For<IDeviceSignalProvider>();
            var androidProvider = Substitute.For<IDeviceSignalProvider>();
            var iosProvider = Substitute.For<IDeviceSignalProvider>();

            var fallbackProvider = Substitute.For<IDeviceSignalProvider>();
            fallbackProvider.GetSignalsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DeviceSignal>>(new List<DeviceSignal>
            {
                DeviceSignal.Available("CpuCoreCount", "Fallback", "8", "cpu_hash")
            }));

            var options = new DeviceOptions();

            var composite = new CompositeDeviceSignalProvider(
                platformDetector,
                options,
                windowsProvider,
                linuxProvider,
                macProvider,
                androidProvider,
                iosProvider,
                fallbackProvider,
                NullLogger<CompositeDeviceSignalProvider>.Instance);

            var signals = await composite.GetSignalsAsync();

            Assert.Contains(signals, s => s.Name == "CpuCoreCount");
        }
    }
}
