using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Device.Management
{
    /// <summary>
    /// Privileged administrative interface for managing registered pharmacy workstations and devices.
    /// Separated from the public client registry interface to ensure least-privilege access and security.
    /// </summary>
    public interface IDeviceManagementService
    {
        /// <summary>
        /// Lists all registered devices belonging to a pharmacy tenant with optional status filtering.
        /// </summary>
        /// <param name="tenantId">Pharmacy tenant identifier.</param>
        /// <param name="statusFilter">Optional lifecycle status filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of matching device records.</returns>
        Task<IReadOnlyList<DeviceRecord>> ListDevicesAsync(
            string tenantId,
            DeviceStatus? statusFilter = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an individual device record by registration ID.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Device record or <c>null</c> if not found.</returns>
        Task<DeviceRecord?> GetDeviceAsync(
            string registrationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Renames a registered workstation display name.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="newDisplayName">New friendly display name.</param>
        /// <param name="operatorId">Administrative operator ID making the change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated device record.</returns>
        Task<DeviceRecord> RenameDeviceAsync(
            string registrationId,
            string newDisplayName,
            string operatorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reassigns a device to a different branch, pharmacy, or assigned user.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="newOwnership">Updated ownership boundary.</param>
        /// <param name="operatorId">Administrative operator ID making the change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated device record.</returns>
        Task<DeviceRecord> ReassignDeviceAsync(
            string registrationId,
            DeviceOwnership newOwnership,
            string operatorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Temporarily suspends a device registration, halting authenticated backend access.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="reason">Administrative explanation for suspension.</param>
        /// <param name="operatorId">Administrative operator ID making the change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated device record in Suspended state.</returns>
        Task<DeviceRecord> SuspendDeviceAsync(
            string registrationId,
            string reason,
            string operatorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reactivates a previously suspended device registration.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="reason">Administrative justification for reactivation.</param>
        /// <param name="operatorId">Administrative operator ID making the change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated device record in Active state.</returns>
        Task<DeviceRecord> ReactivateDeviceAsync(
            string registrationId,
            string reason,
            string operatorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Permanently revokes a device registration. Revoked devices can never be reactivated.
        /// </summary>
        /// <param name="revocation">Revocation request parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated device record in Revoked state.</returns>
        Task<DeviceRecord> RevokeDeviceAsync(
            DeviceRevocation revocation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces an existing device with a new device installation during hardware upgrades.
        /// Marks the old device as Replaced and associates the new installation.
        /// </summary>
        /// <param name="replacement">Replacement request parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated old device record in Replaced state.</returns>
        Task<DeviceRecord> ReplaceDeviceAsync(
            DeviceReplacementRequest replacement,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the immutable audit event log for a given device registration.
        /// </summary>
        /// <param name="registrationId">Server registration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Chronological collection of device audit events.</returns>
        Task<IReadOnlyList<DeviceAuditEvent>> GetAuditEventsAsync(
            string registrationId,
            CancellationToken cancellationToken = default);
    }
}
