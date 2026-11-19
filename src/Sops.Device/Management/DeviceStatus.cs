namespace Sops.Device.Management
{
    /// <summary>
    /// Lifecycle registration state of a device in the SOPs ecosystem.
    /// Distinguishes authoritative server registration status from local hardware detection or license entitlements.
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// Device identity has been computed locally but has not yet been registered with the authoritative backend.
        /// </summary>
        Unregistered = 0,

        /// <summary>
        /// Registration request has been submitted and is awaiting administrative or multi-factor approval.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Device is authoritatively registered, verified, and permitted to interact with SOPs backend services.
        /// </summary>
        Active = 2,

        /// <summary>
        /// Device registration is temporarily suspended (e.g. pending security review, overdue subscription, or operator hold).
        /// </summary>
        Suspended = 3,

        /// <summary>
        /// Device registration has been permanently revoked (e.g. decommissioned, reported lost/stolen, or compromised).
        /// Revoked devices are permanently prohibited from obtaining tokens or licenses.
        /// </summary>
        Revoked = 4,

        /// <summary>
        /// Device registration has been superseded by a replacement device through an authorized hardware migration.
        /// </summary>
        Replaced = 5
    }
}
