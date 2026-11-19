using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Management;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Secure storage abstraction for persisting sensitive device registration credentials and authoritative state.
    /// Strictly separated from non-secret identity storage (<see cref="IDeviceIdentityStore"/>).
    /// </summary>
    public interface IDeviceCredentialStore
    {
        /// <summary>
        /// Reads the persisted device registration credentials and authoritative status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Decrypted local registration record, or <c>null</c> if not yet registered.</returns>
        /// <exception cref="Sops.Device.Persistence.DeviceCredentialStorageException">
        /// Thrown when credential storage is present but cannot be decrypted, accessed, or is corrupted.
        /// Callers must NOT silently generate new credentials upon encountering this exception.
        /// </exception>
        Task<LocalDeviceRegistration?> ReadRegistrationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically and securely persists the device registration record using platform-appropriate protection (e.g. DPAPI on Windows).
        /// </summary>
        /// <param name="registration">Registration record to encrypt and persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveRegistrationAsync(LocalDeviceRegistration registration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears persisted credentials (e.g. upon authorized administrative de-registration).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ClearRegistrationAsync(CancellationToken cancellationToken = default);
    }
}
