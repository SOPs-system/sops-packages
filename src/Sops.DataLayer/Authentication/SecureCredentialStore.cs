using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;

namespace Sops.DataLayer.Authentication
{
    /// <summary>
    /// Custom exception capturing native Windows DPAPI Win32 error codes.
    /// </summary>
    public sealed class DpapiException : Exception
    {
        public int Win32ErrorCode { get; }

        public DpapiException(int win32ErrorCode, string message) : base(message)
        {
            Win32ErrorCode = win32ErrorCode;
        }

        /// <summary>
        /// True if the error indicates permanent, unrecoverable data corruption.
        /// False if the error is caused by a temporary or recoverable profile state (e.g. locked profile, access denied).
        /// </summary>
        public bool IsPermanentCorruption =>
            Win32ErrorCode == 13 || // ERROR_INVALID_DATA
            Win32ErrorCode == unchecked((int)0x80092004) || // CRYPT_E_BAD_DECODE
            Win32ErrorCode == unchecked((int)0x80092005) || // CRYPT_E_NO_DECRYPT_SECRET
            Win32ErrorCode == unchecked((int)0x80090005);   // NTE_BAD_DATA
    }

    /// <summary>
    /// Production-grade DPAPI-backed secure credential store for Windows desktop POS environments.
    /// Encrypts secrets using CryptProtectData with CurrentUser scope and persists encrypted blobs atomically.
    /// Provides safe error recovery against file corruption and locked Windows profiles without destroying recoverable tokens.
    /// </summary>
    public sealed class SecureCredentialStore : ISecureCredentialStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _memoryCache = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<SecureCredentialStore>? _logger;
        private readonly byte[]? _entropy;
        private readonly string? _storageDirectory;
        private readonly object _fileLock = new object();

        public SecureCredentialStore(
            string? entropy = "Sops.DataLayer.Entropy.v1",
            string? storageDirectory = null,
            ILogger<SecureCredentialStore>? logger = null)
        {
            _logger = logger;
            _entropy = string.IsNullOrEmpty(entropy) ? null : Encoding.UTF8.GetBytes(entropy!);

            if (IsWindows())
            {
                string baseDir = !string.IsNullOrWhiteSpace(storageDirectory)
                    ? storageDirectory!
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOPS", "DataLayer", "Tokens");
                _storageDirectory = baseDir;

                try
                {
                    if (!Directory.Exists(_storageDirectory))
                    {
                        Directory.CreateDirectory(_storageDirectory);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not initialize secure token directory at {Path}. Falling back to process memory store.", _storageDirectory);
                    _storageDirectory = null;
                }
            }
        }

        public Task SaveTokenAsync(string key, string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty", nameof(key));

            if (token == null)
            {
                return DeleteTokenAsync(key, cancellationToken);
            }

            try
            {
                byte[] raw = Encoding.UTF8.GetBytes(token);
                byte[] cipher;

                if (IsWindows())
                {
                    cipher = WindowsDpapi.Protect(raw, _entropy);

                    if (_storageDirectory != null)
                    {
                        PersistEncryptedBytes(key, cipher);
                    }
                }
                else
                {
                    cipher = TestObfuscate(raw);
                }

                _memoryCache[key] = cipher;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to protect token for key {Key}", key);
                throw;
            }

            return Task.FromResult(0);
        }

        public Task<string?> GetTokenAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty", nameof(key));

            byte[]? cipher = null;

            if (_memoryCache.TryGetValue(key, out var cachedBytes))
            {
                cipher = cachedBytes;
            }
            else if (IsWindows() && _storageDirectory != null)
            {
                cipher = TryLoadEncryptedBytes(key);
                if (cipher != null)
                {
                    _memoryCache[key] = cipher;
                }
            }

            if (cipher == null)
            {
                return Task.FromResult<string?>(null);
            }

            try
            {
                byte[] raw;
                if (IsWindows())
                {
                    raw = WindowsDpapi.Unprotect(cipher, _entropy);
                }
                else
                {
                    raw = TestDeobfuscate(cipher);
                }

                return Task.FromResult<string?>(Encoding.UTF8.GetString(raw));
            }
            catch (DpapiException dex)
            {
                _logger?.LogWarning(dex, "DPAPI unprotect failed for key {Key} with win32 code {Code}", key, dex.Win32ErrorCode);

                // Only delete the file if permanently corrupted; do NOT delete on temporary/locked profile errors
                if (dex.IsPermanentCorruption)
                {
                    _logger?.LogWarning("Permanent corruption detected for key {Key}. Deleting corrupted file.", key);
                    DeleteTokenFile(key);
                    _memoryCache.TryRemove(key, out _);
                }

                return Task.FromResult<string?>(null);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Unexpected error unprotecting token for key {Key}", key);
                return Task.FromResult<string?>(null);
            }
        }

        public Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _memoryCache.TryRemove(key, out _);

                if (IsWindows() && _storageDirectory != null)
                {
                    DeleteTokenFile(key);
                }
            }
            return Task.FromResult(0);
        }

        private void PersistEncryptedBytes(string key, byte[] cipher)
        {
            if (_storageDirectory == null) return;

            lock (_fileLock)
            {
                string targetPath = GetFilePath(key);
                string tempPath = targetPath + ".tmp." + Guid.NewGuid().ToString("N");

                try
                {
                    File.WriteAllBytes(tempPath, cipher);
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                    File.Move(tempPath, targetPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to persist encrypted token file for key {Key}", key);
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
            }
        }

        private byte[]? TryLoadEncryptedBytes(string key)
        {
            if (_storageDirectory == null) return null;

            lock (_fileLock)
            {
                string path = GetFilePath(key);
                if (!File.Exists(path)) return null;

                try
                {
                    return File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to read encrypted token file for key {Key}", key);
                    return null;
                }
            }
        }

        private void DeleteTokenFile(string key)
        {
            if (_storageDirectory == null) return;

            lock (_fileLock)
            {
                string path = GetFilePath(key);
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch { }
            }
        }

        private string GetFilePath(string key)
        {
            var sb = new StringBuilder();
            foreach (char c in key)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            return Path.Combine(_storageDirectory!, sb.ToString() + ".dat");
        }

        private static bool IsWindows()
        {
#if NETFRAMEWORK
            return true;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }

        private static byte[] TestObfuscate(byte[] data)
        {
            byte[] res = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) res[i] = (byte)(data[i] ^ 0x5A);
            return res;
        }

        private static byte[] TestDeobfuscate(byte[] data)
        {
            return TestObfuscate(data);
        }

        private static class WindowsDpapi
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct DATA_BLOB
            {
                public int cbData;
                public IntPtr pbData;
            }

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool CryptProtectData(
                ref DATA_BLOB pPlainText,
                string szDescription,
                ref DATA_BLOB pEntropy,
                IntPtr pReserved,
                IntPtr pPromptStruct,
                int dwFlags,
                ref DATA_BLOB pCipherText);

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool CryptUnprotectData(
                ref DATA_BLOB pCipherText,
                IntPtr pszDescription,
                ref DATA_BLOB pEntropy,
                IntPtr pReserved,
                IntPtr pPromptStruct,
                int dwFlags,
                ref DATA_BLOB pPlainText);

            [DllImport("kernel32.dll")]
            private static extern IntPtr LocalFree(IntPtr hMem);

            private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

            public static byte[] Protect(byte[] plainText, byte[]? optionalEntropy)
            {
                if (plainText == null) throw new ArgumentNullException(nameof(plainText));

                var plainBlob = new DATA_BLOB();
                var cipherBlob = new DATA_BLOB();
                var entropyBlob = new DATA_BLOB();

                var hPlain = GCHandle.Alloc(plainText, GCHandleType.Pinned);
                GCHandle? hEntropy = null;

                try
                {
                    plainBlob.pbData = hPlain.AddrOfPinnedObject();
                    plainBlob.cbData = plainText.Length;

                    if (optionalEntropy != null && optionalEntropy.Length > 0)
                    {
                        hEntropy = GCHandle.Alloc(optionalEntropy, GCHandleType.Pinned);
                        entropyBlob.pbData = hEntropy.Value.AddrOfPinnedObject();
                        entropyBlob.cbData = optionalEntropy.Length;
                    }

                    bool success = CryptProtectData(ref plainBlob, "SOPS_USER_TOKEN", ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref cipherBlob);
                    if (!success)
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new DpapiException(err, "CryptProtectData failed with win32 error " + err);
                    }

                    byte[] cipherText = new byte[cipherBlob.cbData];
                    Marshal.Copy(cipherBlob.pbData, cipherText, 0, cipherBlob.cbData);
                    return cipherText;
                }
                finally
                {
                    if (hPlain.IsAllocated) hPlain.Free();
                    if (hEntropy.HasValue && hEntropy.Value.IsAllocated) hEntropy.Value.Free();
                    if (cipherBlob.pbData != IntPtr.Zero) LocalFree(cipherBlob.pbData);
                }
            }

            public static byte[] Unprotect(byte[] cipherText, byte[]? optionalEntropy)
            {
                if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));

                var cipherBlob = new DATA_BLOB();
                var plainBlob = new DATA_BLOB();
                var entropyBlob = new DATA_BLOB();

                var hCipher = GCHandle.Alloc(cipherText, GCHandleType.Pinned);
                GCHandle? hEntropy = null;

                try
                {
                    cipherBlob.pbData = hCipher.AddrOfPinnedObject();
                    cipherBlob.cbData = cipherText.Length;

                    if (optionalEntropy != null && optionalEntropy.Length > 0)
                    {
                        hEntropy = GCHandle.Alloc(optionalEntropy, GCHandleType.Pinned);
                        entropyBlob.pbData = hEntropy.Value.AddrOfPinnedObject();
                        entropyBlob.cbData = optionalEntropy.Length;
                    }

                    bool success = CryptUnprotectData(ref cipherBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref plainBlob);
                    if (!success)
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new DpapiException(err, "CryptUnprotectData failed with win32 error " + err);
                    }

                    byte[] plainText = new byte[plainBlob.cbData];
                    Marshal.Copy(plainBlob.pbData, plainText, 0, plainBlob.cbData);
                    return plainText;
                }
                finally
                {
                    if (hCipher.IsAllocated) hCipher.Free();
                    if (hEntropy.HasValue && hEntropy.Value.IsAllocated) hEntropy.Value.Free();
                    if (plainBlob.pbData != IntPtr.Zero) LocalFree(plainBlob.pbData);
                }
            }
        }
    }
}
