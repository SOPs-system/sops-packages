using System.Threading;
using System.Threading.Tasks;

namespace Sops.Device.Management
{
    /// <summary>
    /// Client-facing device registry boundary for workstation registration, verification, and status heartbeats.
    /// Handles idempotent registration, connectivity retries, and authoritative registration validation.
    /// </summary>
    public interface IDeviceRegistry
    {
        /// <summary>
        /// Registers a device installation with the SOPs backend ecosystem idempotently.
        /// Repeated calls with the same <see cref="DeviceRegistrationRequest.IdempotencyToken"/>
        /// safely return the previously established registration result without creating duplicate records.
        /// </summary>
        /// <param name="request">Registration payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Registration outcome including server token and authoritative status.</returns>
        Task<DeviceRegistrationResult> RegisterAsync(
            DeviceRegistrationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current authoritative device record by its registration ID.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Authoritative device record or <c>null</c> if not found.</returns>
        Task<DeviceRecord?> GetAsync(
            string registrationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current authoritative device record by its readable device code.
        /// </summary>
        /// <param name="deviceCode">Crockford Base32 device code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Authoritative device record or <c>null</c> if not found.</returns>
        Task<DeviceRecord?> GetByDeviceCodeAsync(
            string deviceCode,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches a periodic status and liveness heartbeat to the registry.
        /// </summary>
        /// <param name="heartbeat">Heartbeat frame.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Heartbeat response with current server status and instructions.</returns>
        Task<DeviceHeartbeatResult> ReportHeartbeatAsync(
            DeviceHeartbeat heartbeat,
            CancellationToken cancellationToken = default);
    }
}
