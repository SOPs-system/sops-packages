using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Abstractions;
using Sops.Device.Management;

namespace Sops.Device.Persistence
{
    /// <summary>
    /// Thread-safe in-memory implementation of <see cref="IDeviceCredentialStore"/> for testing and ephemeral environments.
    /// </summary>
    public sealed class InMemoryDeviceCredentialStore : IDeviceCredentialStore
    {
        private readonly object _syncLock = new object();
        private LocalDeviceRegistration? _registration;

        public Task<LocalDeviceRegistration?> ReadRegistrationAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                return Task.FromResult(_registration);
            }
        }

        public Task SaveRegistrationAsync(LocalDeviceRegistration registration, CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _registration = registration;
                return Task.CompletedTask;
            }
        }

        public Task ClearRegistrationAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                _registration = null;
                return Task.CompletedTask;
            }
        }
    }
}
