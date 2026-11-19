using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Models;

namespace Sops.Device.Abstractions
{
    /// <summary>
    /// Unified facade for cross-platform device identification, signal collection,
    /// device-code generation, metadata, and hardware-change detection.
    /// </summary>
    public interface IDeviceService
    {
        /// <summary>
        /// Retrieves safe diagnostic and registration metadata about this device.
        /// </summary>
        Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current device identity record.
        /// </summary>
        Task<DeviceIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the stable, formatted device registration and support code (e.g. SOP-WIN-7K2M-9Q4D).
        /// </summary>
        Task<string> GetDeviceCodeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gathers hardware signals and generates a current versioned hardware fingerprint.
        /// </summary>
        Task<DeviceFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Compares a previously recorded fingerprint against the current device state to evaluate hardware drift.
        /// </summary>
        /// <param name="previous">Fingerprint captured during previous activation or registration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Structured comparison result detailing matched, changed, added, and removed hardware components.</returns>
        Task<DeviceIdentityComparison> CompareAsync(DeviceFingerprint previous, CancellationToken cancellationToken = default);
    }
}
