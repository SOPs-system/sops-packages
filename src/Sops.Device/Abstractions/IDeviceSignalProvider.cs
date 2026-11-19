using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Models;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Collects and evaluates hardware and operating system signals for device fingerprinting.
    /// Implementations must not require administrative or root privileges.
    /// </summary>
    public interface IDeviceSignalProvider
    {
        /// <summary>
        /// Gathers available hardware and platform signals.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Read-only collection of evaluated signals.</returns>
        Task<IReadOnlyList<DeviceSignal>> GetSignalsAsync(CancellationToken cancellationToken = default);
    }
}
