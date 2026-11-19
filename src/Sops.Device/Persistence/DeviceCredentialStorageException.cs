using System;

namespace Sops.Device.Persistence
{
    /// <summary>
    /// Exception thrown when accessing, decrypting, or persisting sensitive device registration credentials fails.
    /// Explicitly indicates credential loss or lock contention so callers avoid silently generating duplicate credentials.
    /// </summary>
    public sealed class DeviceCredentialStorageException : Exception
    {
        /// <summary>Whether existing credentials were detected but could not be decrypted or recovered (permanent credential loss).</summary>
        public bool IsCredentialLost { get; }

        public DeviceCredentialStorageException(string message, bool isCredentialLost = false)
            : base(message)
        {
            IsCredentialLost = isCredentialLost;
        }

        public DeviceCredentialStorageException(string message, Exception innerException, bool isCredentialLost = false)
            : base(message, innerException)
        {
            IsCredentialLost = isCredentialLost;
        }
    }
}
