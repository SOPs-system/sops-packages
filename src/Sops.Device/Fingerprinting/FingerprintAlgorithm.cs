namespace Sops.Device.Fingerprinting
{
    /// <summary>
    /// Cryptographic algorithm and hashing strategy used for computing hardware fingerprints.
    /// </summary>
    public enum FingerprintAlgorithm
    {
        /// <summary>
        /// Unkeyed SHA-256 with optional application salt (Version 1 algorithm).
        /// Compatible with legacy license bindings. Susceptible to offline dictionary attacks
        /// against low-entropy hardware signals if salt is known.
        /// </summary>
        Sha256Unkeyed = 1,

        /// <summary>
        /// Keyed HMAC-SHA256 utilizing a securely injected secret key (Version 2 algorithm).
        /// Cryptographically protects predictable hardware signals (such as MAC addresses)
        /// from offline rainbow table or brute-force dictionary attacks.
        /// </summary>
        HmacSha256Keyed = 2
    }
}
