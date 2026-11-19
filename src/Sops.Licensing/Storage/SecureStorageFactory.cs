using System;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;
using Sops.Licensing.Platform;
using Sops.Licensing.Storage.Linux;
using Sops.Licensing.Storage.Mac;
using Sops.Licensing.Storage.Windows;

namespace Sops.Licensing.Storage
{
    /// <summary>
    /// Factory for creating the optimal operating-system-backed secure storage implementation.
    /// Never falls back silently to plaintext; fails closed if no secure backend is available.
    /// </summary>
    public static class SecureStorageFactory
    {
        /// <summary>
        /// Creates an <see cref="ISecureStorage"/> instance based on configured options and runtime operating system detection.
        /// </summary>
        /// <param name="options">The licensing and storage options.</param>
        /// <returns>A concrete <see cref="ISecureStorage"/> provider.</returns>
        public static ISecureStorage CreateStorage(LicensingOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            string ns = string.IsNullOrWhiteSpace(options.StorageNamespace) ? "Sops.Licensing" : options.StorageNamespace;

            // Explicit provider selection
            switch (options.StorageProvider)
            {
                case StorageProviderType.WindowsCredentialManager:
                    if (!PlatformDetector.IsWindows)
                        throw new SecureStorageException("Windows Credential Manager is only supported on Windows.", "WindowsCredentialManager");
                    return new CredentialManagerStorage(ns);

                case StorageProviderType.WindowsDpapi:
                    if (!PlatformDetector.IsWindows)
                        throw new SecureStorageException("Windows DPAPI is only supported on Windows.", "WindowsDpapi");
                    return new DpapiFileStorage(options.StorageDirectory, DpapiScope.CurrentUser, ns);

                case StorageProviderType.MacKeychain:
                    if (!PlatformDetector.IsMacOS)
                        throw new SecureStorageException("macOS Keychain is only supported on macOS.", "MacKeychain");
                    return new MacKeychainStorage(ns);

                case StorageProviderType.LinuxSecretService:
                    if (!PlatformDetector.IsLinux)
                        throw new SecureStorageException("Linux Secret Service is only supported on Linux.", "LinuxSecretService");
                    return new LinuxSecretServiceStorage(ns);

                case StorageProviderType.LinuxEncryptedVault:
                    return new EncryptedVaultStorage(options.StorageDirectory, options.LinuxVaultPassphrase, ns);

                case StorageProviderType.Auto:
                default:
                    return CreateAutoDetectedStorage(options, ns);
            }
        }

        private static ISecureStorage CreateAutoDetectedStorage(LicensingOptions options, string storageNamespace)
        {
            if (PlatformDetector.IsWindows)
            {
                return new HybridWindowsSecureStorage(storageNamespace, options.StorageDirectory);
            }

            if (PlatformDetector.IsMacOS)
            {
                return new MacKeychainStorage(storageNamespace);
            }

            if (PlatformDetector.IsLinux)
            {
                if (LinuxSecretServiceStorage.IsAvailable())
                {
                    return new LinuxSecretServiceStorage(storageNamespace);
                }

                // If headless or Secret Service unavailable, try EncryptedVaultStorage
                try
                {
                    return new EncryptedVaultStorage(options.StorageDirectory, options.LinuxVaultPassphrase, storageNamespace);
                }
                catch (Exception ex)
                {
                    throw new SecureStorageException(
                        "No secure storage backend available on Linux. Secret Service D-Bus is unreachable and EncryptedVaultStorage could not be initialized. Configure a LinuxVaultPassphrase or machine key source.",
                        ex,
                        "LinuxAutoDetect");
                }
            }

            throw new SecureStorageException(
                $"Unsupported or unrecognized operating system ({PlatformDetector.CurrentOS}). No secure storage backend available.",
                "UnsupportedOS");
        }
    }
}
