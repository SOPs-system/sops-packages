namespace Sops.Device.Models
{
    /// <summary>
    /// Configurable mode determining how device identity is derived and evaluated.
    /// </summary>
    public enum DeviceIdentityMode
    {
        /// <summary>
        /// Default mode. Uses a persistent cryptographically random installation identifier.
        /// Guaranteed to remain stable across hardware upgrades while storage persists.
        /// </summary>
        Installation = 0,

        /// <summary>
        /// Derives a versioned fingerprint strictly from available and validated hardware signals.
        /// </summary>
        Hardware = 1,

        /// <summary>
        /// Recommended hybrid mode. Combines stable installation identity with hardware-change detection.
        /// The installation ID remains primary; hardware changes are reported as drift assessments without breaking identity.
        /// </summary>
        Hybrid = 2
    }
}
