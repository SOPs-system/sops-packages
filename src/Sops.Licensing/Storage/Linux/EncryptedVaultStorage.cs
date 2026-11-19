using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Cryptography;
using Sops.Licensing.Models;

namespace Sops.Licensing.Storage.Linux
{
    /// <summary>
    /// Authenticated encrypted file vault for Linux headless environments and containers.
    /// Uses AES-256-CBC with HMAC-SHA256 (Encrypt-then-MAC) and PBKDF2-SHA256 key derivation.
    /// Strictly fails closed if no master passphrase or machine key source is available.
    /// </summary>
    public class EncryptedVaultStorage : ISecureStorage
    {
        private readonly string _storageDirectory;
        private readonly byte[] _masterKey;
        private readonly object _syncLock = new object();

        /// <inheritdoc />
        public string ProviderName => "LinuxEncryptedVault";

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptedVaultStorage"/> class.
        /// </summary>
        /// <param name="storageDirectory">Target vault directory.</param>
        /// <param name="passphrase">Master passphrase or secret for key derivation.</param>
        /// <param name="storageNamespace">Storage namespace (default: "Sops.Licensing").</param>
        public EncryptedVaultStorage(
            string? storageDirectory = null,
            string? passphrase = null,
            string storageNamespace = "Sops.Licensing")
        {
            string baseDir = !string.IsNullOrWhiteSpace(storageDirectory)
                ? storageDirectory!
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sops", "vault");

            _storageDirectory = Path.Combine(baseDir, SanitizeFileName(storageNamespace));

            EnsureDirectoryWithPermissions();

            string effectivePassphrase = ResolvePassphrase(passphrase);
            if (string.IsNullOrWhiteSpace(effectivePassphrase))
            {
                throw new SecureStorageException(
                    "EncryptedVaultStorage requires an explicit master passphrase or machine-secret on headless systems. Cannot operate insecurely.",
                    ProviderName);
            }

            // Derive master key using PBKDF2 (100,000 rounds)
            byte[] salt = Encoding.UTF8.GetBytes($"SOPS_VAULT_SALT_{storageNamespace}");
            using (var pbkdf2 = new Rfc2898DeriveBytes(effectivePassphrase, salt, 100000))
            {
                _masterKey = pbkdf2.GetBytes(64); // 32 bytes AES key + 32 bytes HMAC key
            }
        }

        private static string ResolvePassphrase(string? configuredPassphrase)
        {
            if (!string.IsNullOrWhiteSpace(configuredPassphrase))
            {
                return configuredPassphrase!.Trim();
            }

            // Check environment variable
            string? envPass = Environment.GetEnvironmentVariable("SOPS_VAULT_KEY");
            if (!string.IsNullOrWhiteSpace(envPass))
            {
                return envPass!.Trim();
            }

            // Check /etc/machine-id on Linux
            if (File.Exists("/etc/machine-id"))
            {
                try
                {
                    string machineId = File.ReadAllText("/etc/machine-id").Trim();
                    if (!string.IsNullOrEmpty(machineId)) return machineId;
                }
                catch { }
            }

            return string.Empty;
        }

        private void EnsureDirectoryWithPermissions()
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }

            // Apply chmod 0700 on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    chmod(_storageDirectory, 0x1C0); // 0700: S_IRWXU
                }
                catch { }
            }
        }

        private string GetFilePath(string key)
        {
            string safeKey = SanitizeFileName(key);
            return Path.Combine(_storageDirectory, safeKey + ".vault");
        }

        private static string SanitizeFileName(string input)
        {
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        /// <inheritdoc />
        public Task SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            string path = GetFilePath(key);
            lock (_syncLock)
            {
                if (File.Exists(path))
                {
                    throw new SecureStorageException($"Credential '{key}' already exists in vault.", ProviderName);
                }

                byte[] encrypted = Encrypt(data);
                WriteAtomicFile(path, encrypted);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SaveStringAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return SaveAsync(key, Encoding.UTF8.GetBytes(value), cancellationToken);
        }

        /// <inheritdoc />
        public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));

            string path = GetFilePath(key);
            lock (_syncLock)
            {
                if (!File.Exists(path))
                {
                    return Task.FromResult<byte[]?>(null);
                }

                byte[] encrypted = File.ReadAllBytes(path);
                byte[] decrypted = Decrypt(encrypted);
                return Task.FromResult<byte[]?>(decrypted);
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            byte[]? data = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            return data != null ? Encoding.UTF8.GetString(data) : null;
        }

        /// <inheritdoc />
        public Task UpdateAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            string path = GetFilePath(key);
            lock (_syncLock)
            {
                if (!File.Exists(path))
                {
                    throw new SecureStorageException($"Credential '{key}' does not exist to update in vault.", ProviderName);
                }

                byte[] encrypted = Encrypt(data);
                WriteAtomicFile(path, encrypted);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdateStringAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return UpdateAsync(key, Encoding.UTF8.GetBytes(value), cancellationToken);
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) return Task.FromResult(false);

            string path = GetFilePath(key);
            lock (_syncLock)
            {
                if (!File.Exists(path)) return Task.FromResult(false);

                // Zero out content before deleting
                long len = new FileInfo(path).Length;
                if (len > 0)
                {
                    byte[] zeros = new byte[Math.Min(len, 4096)];
                    File.WriteAllBytes(path, zeros);
                }
                File.Delete(path);
                return Task.FromResult(true);
            }
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) return Task.FromResult(false);
            string path = GetFilePath(key);
            return Task.FromResult(File.Exists(path));
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            var keys = new List<string>();
            lock (_syncLock)
            {
                if (Directory.Exists(_storageDirectory))
                {
                    foreach (string file in Directory.GetFiles(_storageDirectory, "*.vault"))
                    {
                        keys.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
            }
            return Task.FromResult<IReadOnlyList<string>>(keys);
        }

        private byte[] Encrypt(byte[] plaintext)
        {
            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];
            Buffer.BlockCopy(_masterKey, 0, aesKey, 0, 32);
            Buffer.BlockCopy(_masterKey, 32, hmacKey, 0, 32);

            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var enc = aes.CreateEncryptor();
                cipher = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }

            // Payload format: IV (16) || Ciphertext (N) || HMAC (32)
            byte[] payload = new byte[16 + cipher.Length + 32];
            Buffer.BlockCopy(iv, 0, payload, 0, 16);
            Buffer.BlockCopy(cipher, 0, payload, 16, cipher.Length);

            using (var hmac = new HMACSHA256(hmacKey))
            {
                byte[] mac = hmac.ComputeHash(payload, 0, 16 + cipher.Length);
                Buffer.BlockCopy(mac, 0, payload, 16 + cipher.Length, 32);
            }

            return payload;
        }

        private byte[] Decrypt(byte[] payload)
        {
            if (payload.Length < 16 + 32)
            {
                throw new SecureStorageException("Vault payload is truncated or corrupted.", ProviderName);
            }

            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];
            Buffer.BlockCopy(_masterKey, 0, aesKey, 0, 32);
            Buffer.BlockCopy(_masterKey, 32, hmacKey, 0, 32);

            int cipherLen = payload.Length - 16 - 32;

            // Verify HMAC
            using (var hmac = new HMACSHA256(hmacKey))
            {
                byte[] expectedMac = hmac.ComputeHash(payload, 0, 16 + cipherLen);
                byte[] actualMac = new byte[32];
                Buffer.BlockCopy(payload, 16 + cipherLen, actualMac, 0, 32);

                if (!ConstantTime.Equals(expectedMac, actualMac))
                {
                    throw new SecureStorageException("Vault HMAC authentication failed: corrupted or tampered data.", ProviderName);
                }
            }

            byte[] iv = new byte[16];
            Buffer.BlockCopy(payload, 0, iv, 0, 16);

            using (var aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var dec = aes.CreateDecryptor();
                return dec.TransformFinalBlock(payload, 16, cipherLen);
            }
        }

        private static void WriteAtomicFile(string targetPath, byte[] data)
        {
            string tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, data);

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try { chmod(tempPath, 0x180); } catch { } // 0600: S_IRUSR | S_IWUSR
                }

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(tempPath, targetPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);
    }
}
