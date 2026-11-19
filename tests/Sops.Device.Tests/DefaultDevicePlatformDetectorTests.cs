using Sops.Device.Models;
using Sops.Device.Providers;
using Xunit;

namespace Sops.Device.Tests
{
    public class DefaultDevicePlatformDetectorTests
    {
        [Fact]
        public void Detect_ReturnsKnownOrValidPlatform()
        {
            var detector = new DefaultDevicePlatformDetector();
            var platform = detector.Detect();

            // On Windows test environment, this should return Windows
            Assert.NotEqual(DevicePlatform.Unknown, platform);
        }

        [Fact]
        public void Detect_WithMockOverride_ReturnsOverridePlatform()
        {
            var detector = new DefaultDevicePlatformDetector(overridePlatform: DevicePlatform.Linux);
            var platform = detector.Detect();

            Assert.Equal(DevicePlatform.Linux, platform);
        }
    }
}
