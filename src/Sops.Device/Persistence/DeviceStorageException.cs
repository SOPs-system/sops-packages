using System;

namespace Sops.Device.Persistence
{
    /// <summary>
    /// Exception thrown when persistent device identity storage exists but cannot be accessed
    /// due to sharing locks, file permissions, or storage hardware errors.
    /// Prevents silent rotation or replacement of existing identities.
    /// </summary>
    public class DeviceStorageException : Exception
    {
        public string? StoragePath { get; }

        public DeviceStorageException(string message, string? storagePath = null)
            : base(message)
        {
            StoragePath = storagePath;
        }

        public DeviceStorageException(string message, Exception innerException, string? storagePath = null)
            : base(message, innerException)
        {
            StoragePath = storagePath;
        }
    }
}
