using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;

namespace Sops.Licensing.Storage.Windows
{
    /// <summary>
    /// Supported Windows Data Protection API (DPAPI) protection scopes.
    /// </summary>
    public enum DpapiScope
    {
        /// <summary>Protected data is associated with the current user account.</summary>
        CurrentUser,

        /// <summary>Protected data is associated with the local machine context.</summary>
        LocalMachine
    }

    /// <summary>
    /// Secure storage implementation using native Windows DPAPI (CryptProtectData / CryptUnprotectData)
    /// with atomic file write semantics.
    /// Suitable for larger license certificates, tokens, and application secrets.
    /// </summary>
    public class DpapiFileStorage : ISecureStorage
    {
        private readonly string _storageDirectory;
        private readonly DpapiScope _scope;
        private readonly byte[]? _optionalEntropy;
        private readonly object _syncLock = new object();

        /// <inheritdoc />
        public string ProviderName => $"WindowsDpapi({_scope})";

        /// <summary>
        /// Initializes a new instance of the <see cref="DpapiFileStorage"/> class.
        /// </summary>
        /// <param name="storageDirectory">Target directory path. If null, defaults to local AppData folder.</param>
        /// <param name="scope">DPAPI protection scope (CurrentUser or LocalMachine).</param>
        /// <param name="storageNamespace">Namespace directory segment (default: "Sops.Licensing").</param>
        /// <param name="optionalEntropy">Optional extra entropy byte array.</param>
        public DpapiFileStorage(
            string? storageDirectory = null,
            DpapiScope scope = DpapiScope.CurrentUser,
            string storageNamespace = "Sops.Licensing",
            byte[]? optionalEntropy = null)
        {
            _scope = scope;
            _optionalEntropy = optionalEntropy;

            string baseDir = !string.IsNullOrWhiteSpace(storageDirectory)
                ? storageDirectory!
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOPs", "SecureStorage");

            _storageDirectory = Path.Combine(baseDir, SanitizeFileName(storageNamespace));

            EnsureDirectoryExists();
        }

        private string GetFilePath(string key)
        {
            string safeKey = SanitizeFileName(key);
            return Path.Combine(_storageDirectory, safeKey + ".dat");
        }

        private static string SanitizeFileName(string input)
        {
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
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
                    throw new SecureStorageException($"Credential '{key}' already exists in DPAPI file storage.", ProviderName);
                }

                byte[] encrypted = ProtectBytes(data);
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
                byte[] decrypted = UnprotectBytes(encrypted);
                return Task.FromResult<byte[]?>(decrypted);
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            byte[]? bytes = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
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
                    throw new SecureStorageException($"Credential '{key}' does not exist to update in DPAPI storage.", ProviderName);
                }

                byte[] encrypted = ProtectBytes(data);
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
                if (!File.Exists(path))
                {
                    return Task.FromResult(false);
                }

                try
                {
                    // Overwrite with zeros before deletion
                    long len = new FileInfo(path).Length;
                    if (len > 0)
                    {
                        byte[] zeros = new byte[Math.Min(len, 4096)];
                        File.WriteAllBytes(path, zeros);
                    }
                    File.Delete(path);
                    return Task.FromResult(true);
                }
                catch
                {
                    if (File.Exists(path)) File.Delete(path);
                    return Task.FromResult(true);
                }
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
                    foreach (string file in Directory.GetFiles(_storageDirectory, "*.dat"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        keys.Add(name);
                    }
                }
            }
            return Task.FromResult<IReadOnlyList<string>>(keys);
        }

        private static void WriteAtomicFile(string targetPath, byte[] data)
        {
            string tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, data);

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

        private byte[] ProtectBytes(byte[] plaintext)
        {
            var inBlob = new NativeMethods.DATA_BLOB();
            var outBlob = new NativeMethods.DATA_BLOB();

            IntPtr pIn = IntPtr.Zero;
            IntPtr pEntropyBlob = IntPtr.Zero;
            IntPtr pEntropyBytes = IntPtr.Zero;

            try
            {
                pIn = Marshal.AllocHGlobal(plaintext.Length);
                Marshal.Copy(plaintext, 0, pIn, plaintext.Length);
                inBlob.cbData = plaintext.Length;
                inBlob.pbData = pIn;

                int dwFlags = NativeMethods.CRYPTPROTECT_UI_FORBIDDEN;
                if (_scope == DpapiScope.LocalMachine)
                {
                    dwFlags |= NativeMethods.CRYPTPROTECT_LOCAL_MACHINE;
                }

                if (_optionalEntropy != null && _optionalEntropy.Length > 0)
                {
                    pEntropyBytes = Marshal.AllocHGlobal(_optionalEntropy.Length);
                    Marshal.Copy(_optionalEntropy, 0, pEntropyBytes, _optionalEntropy.Length);

                    var entropyBlob = new NativeMethods.DATA_BLOB
                    {
                        cbData = _optionalEntropy.Length,
                        pbData = pEntropyBytes
                    };

                    pEntropyBlob = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.DATA_BLOB)));
                    Marshal.StructureToPtr(entropyBlob, pEntropyBlob, false);
                }

                bool success = NativeMethods.CryptProtectData(
                    ref inBlob,
                    "SOPs DPAPI Secret",
                    pEntropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    dwFlags,
                    out outBlob);

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new SecureStorageException($"CryptProtectData failed with error code: {err}.", new Win32Exception(err), ProviderName);
                }

                byte[] cipher = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, cipher, 0, outBlob.cbData);
                return cipher;
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                if (pEntropyBytes != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBytes);
                if (pEntropyBlob != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBlob);
                if (outBlob.pbData != IntPtr.Zero) NativeMethods.LocalFree(outBlob.pbData);
            }
        }

        private byte[] UnprotectBytes(byte[] ciphertext)
        {
            var inBlob = new NativeMethods.DATA_BLOB();
            var outBlob = new NativeMethods.DATA_BLOB();

            IntPtr pIn = IntPtr.Zero;
            IntPtr pEntropyBlob = IntPtr.Zero;
            IntPtr pEntropyBytes = IntPtr.Zero;

            try
            {
                pIn = Marshal.AllocHGlobal(ciphertext.Length);
                Marshal.Copy(ciphertext, 0, pIn, ciphertext.Length);
                inBlob.cbData = ciphertext.Length;
                inBlob.pbData = pIn;

                int dwFlags = NativeMethods.CRYPTPROTECT_UI_FORBIDDEN;

                if (_optionalEntropy != null && _optionalEntropy.Length > 0)
                {
                    pEntropyBytes = Marshal.AllocHGlobal(_optionalEntropy.Length);
                    Marshal.Copy(_optionalEntropy, 0, pEntropyBytes, _optionalEntropy.Length);

                    var entropyBlob = new NativeMethods.DATA_BLOB
                    {
                        cbData = _optionalEntropy.Length,
                        pbData = pEntropyBytes
                    };

                    pEntropyBlob = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.DATA_BLOB)));
                    Marshal.StructureToPtr(entropyBlob, pEntropyBlob, false);
                }

                bool success = NativeMethods.CryptUnprotectData(
                    ref inBlob,
                    IntPtr.Zero,
                    pEntropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    dwFlags,
                    out outBlob);

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new SecureStorageException($"CryptUnprotectData failed with error code: {err}.", new Win32Exception(err), ProviderName);
                }

                byte[] plain = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, plain, 0, outBlob.cbData);
                return plain;
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                if (pEntropyBytes != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBytes);
                if (pEntropyBlob != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBlob);
                if (outBlob.pbData != IntPtr.Zero) NativeMethods.LocalFree(outBlob.pbData);
            }
        }

        private static class NativeMethods
        {
            public const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;
            public const int CRYPTPROTECT_LOCAL_MACHINE = 0x4;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct DATA_BLOB
            {
                public int cbData;
                public IntPtr pbData;
            }

            [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CryptProtectData(
                ref DATA_BLOB pDataIn,
                string szDataDescr,
                IntPtr pOptionalEntropy,
                IntPtr pvReserved,
                IntPtr pPromptStruct,
                int dwFlags,
                out DATA_BLOB pDataOut);

            [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CryptUnprotectData(
                ref DATA_BLOB pDataIn,
                IntPtr ppszDataDescr,
                IntPtr pOptionalEntropy,
                IntPtr pvReserved,
                IntPtr pPromptStruct,
                int dwFlags,
                out DATA_BLOB pDataOut);

            [DllImport("Kernel32.dll", SetLastError = true)]
            public static extern IntPtr LocalFree(IntPtr hMem);
        }
    }
}
