using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Models;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Thread-safe and process-safe storage provider for persistent device identity.
    /// Supports atomic writes and detects file corruption.
    /// </summary>
    public interface IDeviceIdentityStore
    {
        /// <summary>
        /// Reads the persisted installation UUID if available.
        /// </summary>
        Task<string?> ReadInstallationIdAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists the installation UUID atomically.
        /// </summary>
        Task SaveInstallationIdAsync(string installationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the full persisted device identity record if available.
        /// </summary>
        Task<DeviceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists the complete device identity record atomically.
        /// </summary>
        Task SaveIdentityAsync(DeviceIdentity identity, CancellationToken cancellationToken = default);
    }
}
