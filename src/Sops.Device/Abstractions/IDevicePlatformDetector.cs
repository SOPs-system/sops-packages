using Sops.Device.Models;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Dynamically detects the underlying host operating system platform.
    /// Safe to call on any supported system without throwing exceptions.
    /// </summary>
    public interface IDevicePlatformDetector
    {
        /// <summary>
        /// Detects and returns the active runtime platform.
        /// </summary>
        DevicePlatform Detect();
    }
}
