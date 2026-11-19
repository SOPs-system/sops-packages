using System;
using System.IO;
using System.Threading.Tasks;
using Sops.Device.Models;
using Sops.Device.Persistence;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceIdentityStoreTests
    {
        [Fact]
        public async Task InMemoryStore_SaveAndRead_Succeeds()
        {
            var store = new InMemoryDeviceIdentityStore();
            string installId = Guid.NewGuid().ToString("D");
            var identity = new DeviceIdentity(installId, "SOP-WIN-TEST-1234", DeviceIdentityMode.Hybrid, DevicePlatform.Windows);

            await store.SaveIdentityAsync(identity);
            var loaded = await store.ReadIdentityAsync();

            Assert.NotNull(loaded);
            Assert.Equal(identity.DeviceCode, loaded.DeviceCode);
            Assert.Equal(identity.InstallationId, loaded.InstallationId);
            Assert.Equal(identity.Platform, loaded.Platform);
            Assert.Equal(identity.Mode, loaded.Mode);
        }

        [Fact]
        public async Task FileStore_SaveAndRead_RoundTripsSuccessfully()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_device_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "identity.json");

            try
            {
                var store = new FileDeviceIdentityStore(filePath);
                string installId = Guid.NewGuid().ToString("D");
                var identity = new DeviceIdentity(installId, "SOP-LNX-ABCD-5678", DeviceIdentityMode.Installation, DevicePlatform.Linux);

                await store.SaveIdentityAsync(identity);
                Assert.True(File.Exists(filePath));

                var loaded = await store.ReadIdentityAsync();
                Assert.NotNull(loaded);
                Assert.Equal(identity.DeviceCode, loaded.DeviceCode);
                Assert.Equal(identity.InstallationId, loaded.InstallationId);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task FileStore_CorruptJson_IsolatesToCorruptFileAndReturnsNull()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_device_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "identity.json");

            try
            {
                Directory.CreateDirectory(tempDir);
                // Write garbage data
                await File.WriteAllTextAsync(filePath, "{ this is not valid json! }");

                var store = new FileDeviceIdentityStore(filePath);
                var loaded = await store.ReadIdentityAsync();

                Assert.Null(loaded);
                // Original file should be gone/renamed
                Assert.False(File.Exists(filePath));
                // .corrupt.* file should exist
                var corruptFiles = Directory.GetFiles(tempDir, "identity.json.corrupt*");
                Assert.NotEmpty(corruptFiles);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task FileStore_ChecksumMismatch_IsolatesToCorruptFileAndReturnsNull()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_device_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "identity.json");

            try
            {
                Directory.CreateDirectory(tempDir);
                // Valid envelope JSON structure with an invalid checksum
                string tamperedEnvelope = "{\"InstallationId\": \"test-guid-1234\", \"Checksum\": \"bad_checksum\"}";
                await File.WriteAllTextAsync(filePath, tamperedEnvelope);

                var store = new FileDeviceIdentityStore(filePath);
                var loaded = await store.ReadIdentityAsync();

                Assert.Null(loaded);
                var corruptFiles = Directory.GetFiles(tempDir, "identity.json.corrupt*");
                Assert.NotEmpty(corruptFiles);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task FileStore_ConcurrentAccess_HandledSafely()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_device_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "identity.json");

            try
            {
                var store = new FileDeviceIdentityStore(filePath);
                var tasks = new Task[10];

                for (int i = 0; i < 10; i++)
                {
                    int index = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        string installId = Guid.NewGuid().ToString("D");
                        var id = new DeviceIdentity(installId, $"SOP-WIN-TEST-{index:D4}", DeviceIdentityMode.Hardware, DevicePlatform.Windows);
                        await store.SaveIdentityAsync(id);
                        var loaded = await store.ReadIdentityAsync();
                        Assert.NotNull(loaded);
                    });
                }

                await Task.WhenAll(tasks);
                var finalLoaded = await store.ReadIdentityAsync();
                Assert.NotNull(finalLoaded);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task FileStore_ThrowsDeviceStorageException_WhenFileIsLockedExclusively()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_device_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "identity.json");

            try
            {
                Directory.CreateDirectory(tempDir);
                await File.WriteAllTextAsync(filePath, "{\"InstallationId\":\"test-guid\"}");

                // Lock exclusively with FileShare.None
                using (var exclusiveLock = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var store = new FileDeviceIdentityStore(filePath, maxRetries: 2, retryDelayMs: 20);

                    // When an existing identity file is locked, store MUST throw DeviceStorageException
                    // instead of returning null and silently creating a replacement identity!
                    await Assert.ThrowsAsync<DeviceStorageException>(() => store.ReadIdentityAsync());
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
