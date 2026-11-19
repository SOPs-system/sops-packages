using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Abstractions;
using Sops.Device.Models;

namespace Sops.Device.Persistence
{
    /// <summary>
    /// In-memory implementation of <see cref="IDeviceIdentityStore"/> for unit testing and ephemeral environments.
    /// </summary>
    public sealed class InMemoryDeviceIdentityStore : IDeviceIdentityStore
    {
        private string? _installationId;
        private DeviceIdentity? _identity;
        private readonly object _syncLock = new object();

        public InMemoryDeviceIdentityStore(string? initialInstallationId = null, DeviceIdentity? initialIdentity = null)
        {
            _installationId = initialInstallationId;
            _identity = initialIdentity;
        }

        public Task<string?> ReadInstallationIdAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                return Task.FromResult(_installationId);
            }
        }

        public Task SaveInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _installationId = installationId;
                return Task.FromResult(0);
            }
        }

        public Task<DeviceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                return Task.FromResult(_identity);
            }
        }

        public Task SaveIdentityAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _identity = identity;
                return Task.FromResult(0);
            }
        }
    }
}
