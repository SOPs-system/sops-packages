namespace Sops.Device.Fingerprinting
{
    /// <summary>
    /// Scoping boundary for secret keys used in cryptographic hardware fingerprinting.
    /// Scoping isolates hardware signatures so that the same physical machine cannot be
    /// correlated across distinct tenants, pharmacies, or environments.
    /// </summary>
    public enum KeyScope
    {
        /// <summary>
        /// Key is generated and stored locally per application installation.
        /// </summary>
        Installation = 0,

        /// <summary>
        /// Key is shared across the host application deployment.
        /// </summary>
        Application = 1,

        /// <summary>
        /// Key is provisioned per pharmacy tenant or organization.
        /// Prevents cross-tenant device correlation and rainbow table precomputation.
        /// </summary>
        Tenant = 2,

        /// <summary>
        /// Key is held exclusively on the authoritative backend server.
        /// Client computes unkeyed or local signals; server computes authoritative keyed fingerprint.
        /// </summary>
        Server = 3
    }
}
