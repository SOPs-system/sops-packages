using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Sops.Licensing.Models;
using Sops.Licensing.Platform;
using Sops.Licensing.Storage.Windows;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class SecureStorageTests
    {
        [Fact]
        public async Task DpapiFileStorage_CurrentUserScope_PerformsCrud()
        {
            if (!PlatformDetector.IsWindows) return;

            string tempDir = Path.Combine(Path.GetTempPath(), "sops_test_dpapi_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "TestNamespace");
                string key = "test.secret";
                string secret = "MyConfidentialData123!@#";

                // Save
                await storage.SaveStringAsync(key, secret);
                Assert.True(await storage.ExistsAsync(key));

                // Duplicate save throws
                await Assert.ThrowsAsync<SecureStorageException>(() => storage.SaveStringAsync(key, secret));

                // Get
                string? retrieved = await storage.GetStringAsync(key);
                Assert.Equal(secret, retrieved);

                // Update
                string updatedSecret = "UpdatedSecret456$%^";
                await storage.UpdateStringAsync(key, updatedSecret);
                Assert.Equal(updatedSecret, await storage.GetStringAsync(key));

                // List keys
                var keys = await storage.ListKeysAsync();
                Assert.Contains("test.secret", keys);

                // Delete
                bool deleted = await storage.DeleteAsync(key);
                Assert.True(deleted);
                Assert.False(await storage.ExistsAsync(key));
                Assert.Null(await storage.GetStringAsync(key));
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
        public async Task DpapiFileStorage_WithOptionalEntropy_ProtectsAndRestoresData()
        {
            if (!PlatformDetector.IsWindows) return;

            string tempDir = Path.Combine(Path.GetTempPath(), "sops_test_entropy_" + Guid.NewGuid().ToString("N"));
            try
            {
                byte[] entropy = Encoding.UTF8.GetBytes("CustomAppEntropy_Salt_999");
                var storage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "EntropyNs", entropy);

                byte[] payload = Encoding.UTF8.GetBytes("DataToProtectWithExtraEntropy");
                await storage.SaveAsync("entropy.key", payload);

                byte[]? decrypted = await storage.GetAsync("entropy.key");
                Assert.NotNull(decrypted);
                Assert.Equal(payload, decrypted!);

                // A storage with wrong entropy should fail to unprotect
                byte[] wrongEntropy = Encoding.UTF8.GetBytes("DifferentEntropy");
                var wrongStorage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "EntropyNs", wrongEntropy);
                await Assert.ThrowsAsync<SecureStorageException>(() => wrongStorage.GetAsync("entropy.key"));
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
        public async Task CredentialManagerStorage_SmallSecrets_PerformsCrud()
        {
            if (!PlatformDetector.IsWindows || !CredentialManagerStorage.IsAvailable()) return;

            string testKey = "sops.unit.test." + Guid.NewGuid().ToString("N").Substring(0, 8);
            var storage = new CredentialManagerStorage("SopsTestNs");

            try
            {
                string secret = "ApiKey_SuperSecretToken_789";

                // Save
                await storage.SaveStringAsync(testKey, secret);
                Assert.True(await storage.ExistsAsync(testKey));

                // Duplicate save throws
                await Assert.ThrowsAsync<SecureStorageException>(() => storage.SaveStringAsync(testKey, secret));

                // Get
                string? retrieved = await storage.GetStringAsync(testKey);
                Assert.Equal(secret, retrieved);

                // Update
                string updated = "ApiKey_UpdatedToken_999";
                await storage.UpdateStringAsync(testKey, updated);
                Assert.Equal(updated, await storage.GetStringAsync(testKey));

                // Delete
                bool deleted = await storage.DeleteAsync(testKey);
                Assert.True(deleted);
                Assert.False(await storage.ExistsAsync(testKey));
                Assert.Null(await storage.GetStringAsync(testKey));
            }
            finally
            {
                await storage.DeleteAsync(testKey);
            }
        }

        [Fact]
        public async Task HybridWindowsSecureStorage_RoutesByPayloadSize()
        {
            if (!PlatformDetector.IsWindows) return;

            string tempDir = Path.Combine(Path.GetTempPath(), "sops_test_hybrid_" + Guid.NewGuid().ToString("N"));
            string smallKey = "hybrid.small." + Guid.NewGuid().ToString("N").Substring(0, 8);
            string largeKey = "hybrid.large." + Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                var hybrid = new HybridWindowsSecureStorage("SopsHybridNs", tempDir);

                // Small payload (<= 512 bytes)
                byte[] smallData = Encoding.UTF8.GetBytes("SmallSecretToken123");
                await hybrid.SaveAsync(smallKey, smallData);
                Assert.True(await hybrid.ExistsAsync(smallKey));

                byte[]? retrievedSmall = await hybrid.GetAsync(smallKey);
                Assert.NotNull(retrievedSmall);
                Assert.Equal(smallData, retrievedSmall!);

                // Large payload (> 512 bytes)
                byte[] largeData = new byte[1024];
                new Random(42).NextBytes(largeData);

                await hybrid.SaveAsync(largeKey, largeData);
                Assert.True(await hybrid.ExistsAsync(largeKey));

                byte[]? retrievedLarge = await hybrid.GetAsync(largeKey);
                Assert.NotNull(retrievedLarge);
                Assert.Equal(largeData, retrievedLarge!);

                // Clean up
                await hybrid.DeleteAsync(smallKey);
                await hybrid.DeleteAsync(largeKey);
                Assert.False(await hybrid.ExistsAsync(smallKey));
                Assert.False(await hybrid.ExistsAsync(largeKey));
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
        public async Task SecureStorage_ConcurrentOperations_ThreadSafe()
        {
            if (!PlatformDetector.IsWindows) return;

            string tempDir = Path.Combine(Path.GetTempPath(), "sops_test_concurrent_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new DpapiFileStorage(tempDir, DpapiScope.CurrentUser, "ConcurrentNs");

                var tasks = new Task[20];
                for (int i = 0; i < tasks.Length; i++)
                {
                    int index = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        string k = $"key_{index}";
                        string v = $"val_{index}";
                        await storage.SaveStringAsync(k, v);
                        Assert.Equal(v, await storage.GetStringAsync(k));
                    });
                }

                await Task.WhenAll(tasks);

                var keys = await storage.ListKeysAsync();
                Assert.Equal(20, keys.Count);
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
