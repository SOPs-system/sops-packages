using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Sops.Device.Management;
using Sops.Device.Persistence;
using Xunit;

namespace Sops.Device.Tests
{
    public class ProtectedDeviceCredentialStoreTests
    {
        [Fact]
        public async Task SaveAndReadRegistration_RoundTrip_PreservesAllFields()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_cred_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "device_credentials.dat");

            try
            {
                var store = new ProtectedFileDeviceCredentialStore(filePath);
                var reg = new LocalDeviceRegistration(
                    installationId: "00000000-0000-0000-0000-000000000001",
                    deviceCode: "SOP-WIN-742G-K92P-X",
                    registrationId: "REG-9999",
                    lastKnownStatus: DeviceStatus.Active,
                    serverRegistrationToken: "SECRET_SERVER_TOKEN_XYZ",
                    lastSyncAtUtc: DateTimeOffset.UtcNow,
                    cachedAtUtc: DateTimeOffset.UtcNow,
                    tenantId: "tenant-alpha",
                    branchId: "branch-01");

                await store.SaveRegistrationAsync(reg);

                var loaded = await store.ReadRegistrationAsync();

                Assert.NotNull(loaded);
                Assert.Equal(reg.InstallationId, loaded!.InstallationId);
                Assert.Equal(reg.DeviceCode, loaded.DeviceCode);
                Assert.Equal(reg.RegistrationId, loaded.RegistrationId);
                Assert.Equal(reg.LastKnownStatus, loaded.LastKnownStatus);
                Assert.Equal(reg.ServerRegistrationToken, loaded.ServerRegistrationToken);
                Assert.Equal(reg.TenantId, loaded.TenantId);
                Assert.Equal(reg.BranchId, loaded.BranchId);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task ReadRegistration_NonExistentFile_ReturnsNull()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_cred_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "missing.dat");

            try
            {
                var store = new ProtectedFileDeviceCredentialStore(filePath);
                var result = await store.ReadRegistrationAsync();
                Assert.Null(result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task ReadRegistration_CorruptFile_ThrowsDeviceCredentialStorageExceptionWithCredentialLost()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_cred_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "corrupted.dat");

            try
            {
                Directory.CreateDirectory(tempDir);
                File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes("INVALID_ENCRYPTED_DATA_BYTES_CORRUPTED"));

                var store = new ProtectedFileDeviceCredentialStore(filePath);

                var ex = await Assert.ThrowsAsync<DeviceCredentialStorageException>(() => store.ReadRegistrationAsync());
                Assert.True(ex.IsCredentialLost);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task SaveAndRead_WithLocalMachineDpapiScope_RoundTripsSuccessfully()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_cred_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "device_credentials_lm.dat");

            try
            {
                var store = new ProtectedFileDeviceCredentialStore(filePath, protectionScope: DpapiProtectionScope.LocalMachine);
                var reg = new LocalDeviceRegistration(
                    installationId: "00000000-0000-0000-0000-000000000002",
                    deviceCode: "SOP-WIN-LM01-K92P-X",
                    registrationId: "REG-LM-01",
                    lastKnownStatus: DeviceStatus.Active,
                    serverRegistrationToken: "SECRET_LM_TOKEN",
                    lastSyncAtUtc: DateTimeOffset.UtcNow,
                    cachedAtUtc: DateTimeOffset.UtcNow,
                    tenantId: "tenant-lm");

                await store.SaveRegistrationAsync(reg);
                var loaded = await store.ReadRegistrationAsync();

                Assert.NotNull(loaded);
                Assert.Equal("REG-LM-01", loaded!.RegistrationId);
                Assert.Equal("SECRET_LM_TOKEN", loaded.ServerRegistrationToken);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task ClearRegistrationAsync_RemovesFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_cred_test_" + Guid.NewGuid().ToString("N"));
            string filePath = Path.Combine(tempDir, "device_credentials.dat");

            try
            {
                var store = new ProtectedFileDeviceCredentialStore(filePath);
                var reg = LocalDeviceRegistration.Unregistered("00000000-0000-0000-0000-000000000001", "SOP-WIN-742G-K92P-X");
                await store.SaveRegistrationAsync(reg);
                Assert.True(File.Exists(filePath));

                await store.ClearRegistrationAsync();
                Assert.False(File.Exists(filePath));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
