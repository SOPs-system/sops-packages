using System;

namespace Sops.Licensing.Models
{
    /// <summary>
    /// Base exception thrown when a licensing, activation, validation, or cryptographic error occurs.
    /// </summary>
    public class LicensingException : Exception
    {
        /// <summary>Creates a new instance of <see cref="LicensingException"/>.</summary>
        public LicensingException(string message) : base(message) { }

        /// <summary>Creates a new instance of <see cref="LicensingException"/> with an inner exception.</summary>
        public LicensingException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when an application attempts an operation that is not granted by the active license certificate.
    /// </summary>
    public class EntitlementDeniedException : LicensingException
    {
        /// <summary>Gets the entitlement key that was denied.</summary>
        public string EntitlementKey { get; }

        /// <summary>Creates a new instance of <see cref="EntitlementDeniedException"/>.</summary>
        public EntitlementDeniedException(string entitlementKey, string message)
            : base(message)
        {
            EntitlementKey = entitlementKey;
        }
    }

    /// <summary>
    /// Exception thrown when secure credential storage fails or a secure backend is unavailable.
    /// Fails closed rather than compromising secret material.
    /// </summary>
    public class SecureStorageException : Exception
    {
        /// <summary>Gets the name of the storage provider where the error occurred.</summary>
        public string? ProviderName { get; }

        /// <summary>Creates a new instance of <see cref="SecureStorageException"/>.</summary>
        public SecureStorageException(string message, string? providerName = null)
            : base(message)
        {
            ProviderName = providerName;
        }

        /// <summary>Creates a new instance of <see cref="SecureStorageException"/> with an inner exception.</summary>
        public SecureStorageException(string message, Exception innerException, string? providerName = null)
            : base(message, innerException)
        {
            ProviderName = providerName;
        }
    }
}
