using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Sops.Licensing.Models;
using Sops.Licensing.Storage.Linux;
using Xunit;

namespace Sops.Licensing.Tests
{
    public class LinuxVaultStorageTests
    {
        [Fact]
        public async Task EncryptedVaultStorage_WithPassphrase_PerformsAuthenticatedCrud()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_vault_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new EncryptedVaultStorage(tempDir, "StrongPassphrase_999#@!", "VaultTestNs");
                string key = "app.db.password";
                string secret = "P@ssw0rd_Database_Secret!";

                // Save
                await storage.SaveStringAsync(key, secret);
                Assert.True(await storage.ExistsAsync(key));

                // Duplicate save throws
                await Assert.ThrowsAsync<SecureStorageException>(() => storage.SaveStringAsync(key, secret));

                // Get
                string? retrieved = await storage.GetStringAsync(key);
                Assert.Equal(secret, retrieved);

                // Update
                string updated = "New_Database_Secret#123";
                await storage.UpdateStringAsync(key, updated);
                Assert.Equal(updated, await storage.GetStringAsync(key));

                // List
                var keys = await storage.ListKeysAsync();
                Assert.Contains("app.db.password", keys);

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
        public async Task EncryptedVaultStorage_TamperedData_FailsHmacVerification()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_vault_tamper_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new EncryptedVaultStorage(tempDir, "IntegrityKey_12345", "TamperNs");
                string key = "tamper.test";
                await storage.SaveStringAsync(key, "OriginalUntamperedSecret");

                // Locate the stored file
                string filePath = Path.Combine(tempDir, "TamperNs", "tamper.test.vault");
                Assert.True(File.Exists(filePath));

                // Tamper with bytes
                byte[] bytes = File.ReadAllBytes(filePath);
                bytes[20] ^= 0xFF; // Flip byte inside ciphertext
                File.WriteAllBytes(filePath, bytes);

                // Attempt to read should fail with SecureStorageException (HMAC failed)
                await Assert.ThrowsAsync<SecureStorageException>(() => storage.GetStringAsync(key));
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
        public async Task EncryptedVaultStorage_WrongPassphrase_FailsDecryption()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_vault_wrongpass_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage1 = new EncryptedVaultStorage(tempDir, "CorrectPassphrase_1", "PassNs");
                await storage1.SaveStringAsync("token", "ConfidentialValue");

                // Initialize second storage instance with wrong passphrase
                var storage2 = new EncryptedVaultStorage(tempDir, "WrongPassphrase_2", "PassNs");
                await Assert.ThrowsAsync<SecureStorageException>(() => storage2.GetStringAsync("token"));
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
        public void EncryptedVaultStorage_MissingPassphraseAndMachineId_FailsClosed()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_vault_nopass_" + Guid.NewGuid().ToString("N"));
            // When passphrase is null and not on Linux with /etc/machine-id
            if (!File.Exists("/etc/machine-id"))
            {
                Assert.Throws<SecureStorageException>(() => new EncryptedVaultStorage(tempDir, null, "NoPassNs"));
            }
        }
    }
}
