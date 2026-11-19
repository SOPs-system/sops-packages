using System;
using Sops.Licensing.Models;
using Sops.Licensing.Platform;
using Sops.Licensing.Storage;
using Sops.Licensing.Storage.Mac;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class PlatformDetectionTests
    {
        [Fact]
        public void PlatformDetector_IdentifiesCurrentEnvironment()
        {
            var os = PlatformDetector.CurrentOS;
            Assert.NotEqual(OperatingSystemType.Unknown, os);

            if (PlatformDetector.IsWindows)
            {
                Assert.Equal(OperatingSystemType.Windows, os);
                Assert.False(PlatformDetector.IsLinux);
                Assert.False(PlatformDetector.IsMacOS);
            }
            else if (PlatformDetector.IsLinux)
            {
                Assert.Equal(OperatingSystemType.Linux, os);
                Assert.False(PlatformDetector.IsWindows);
                Assert.False(PlatformDetector.IsMacOS);
            }
            else if (PlatformDetector.IsMacOS)
            {
                Assert.Equal(OperatingSystemType.MacOS, os);
                Assert.False(PlatformDetector.IsWindows);
                Assert.False(PlatformDetector.IsLinux);
            }

            Assert.Equal(IntPtr.Size == 8, PlatformDetector.Is64BitProcess);
        }

        [Fact]
        public void MacKeychainStorage_OnNonMacPlatform_FailsClosed()
        {
            if (!PlatformDetector.IsMacOS)
            {
                var storage = new MacKeychainStorage("TestService");
                Assert.ThrowsAsync<SecureStorageException>(() => storage.SaveStringAsync("k", "v"));
            }
        }

        [Fact]
        public void SecureStorageFactory_AutoSelectsSupportedBackend()
        {
            var options = new LicensingOptions
            {
                StorageProvider = StorageProviderType.Auto,
                StorageNamespace = "FactoryAutoTest"
            };

            var storage = SecureStorageFactory.CreateStorage(options);
            Assert.NotNull(storage);
            Assert.NotEmpty(storage.ProviderName);

            if (PlatformDetector.IsWindows)
            {
                Assert.Contains("Windows", storage.ProviderName);
            }
        }

        [Fact]
        public void SecureStorageFactory_MismatchedExplicitProvider_FailsClosed()
        {
            if (PlatformDetector.IsWindows)
            {
                var options = new LicensingOptions
                {
                    StorageProvider = StorageProviderType.MacKeychain
                };

                Assert.Throws<SecureStorageException>(() => SecureStorageFactory.CreateStorage(options));
            }
        }
    }
}
