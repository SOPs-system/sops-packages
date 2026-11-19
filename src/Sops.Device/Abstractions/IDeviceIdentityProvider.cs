using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Models;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Coordinates retrieval, initialization, and caching of the active device identity.
    /// </summary>
    public interface IDeviceIdentityProvider
    {
        /// <summary>
        /// Retrieves the current device identity, initializing and persisting it on first run.
        /// </summary>
        Task<DeviceIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);
    }
}
