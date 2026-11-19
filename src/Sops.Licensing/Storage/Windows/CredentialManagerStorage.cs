using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;

namespace Sops.Licensing.Storage.Windows
{
    /// <summary>
    /// Secure storage implementation backed by Windows Credential Manager using native Advapi32 APIs.
    /// Ideal for small credentials, activation tokens, and encryption keys.
    /// </summary>
    public class CredentialManagerStorage : ISecureStorage
    {
        private readonly string _namespace;
        private readonly object _syncLock = new object();
        private static bool? _isAvailable;
        private static readonly object _availabilityLock = new object();

        /// <inheritdoc />
        public string ProviderName => "WindowsCredentialManager";

        /// <summary>
        /// Maximum recommended credential blob size in bytes for Windows Credential Manager.
        /// </summary>
        public const int MaxBlobSize = 2560; // 2.5 KB limit in Credential Manager

        /// <summary>
        /// Checks whether Windows Credential Manager is available and accessible in the current logon session.
        /// Returns false in sandboxed, non-interactive, or restricted service environments.
        /// </summary>
        public static bool IsAvailable()
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            lock (_availabilityLock)
            {
                if (_isAvailable.HasValue) return _isAvailable.Value;

                if (!Platform.PlatformDetector.IsWindows)
                {
                    _isAvailable = false;
                    return false;
                }

                try
                {
                    string probeTarget = $"SopsProbe_{Guid.NewGuid():N}";
                    byte[] probeData = new byte[] { 1 };
                    IntPtr blobPtr = Marshal.AllocHGlobal(1);
                    try
                    {
                        Marshal.Copy(probeData, 0, blobPtr, 1);
                        var cred = new NativeMethods.CREDENTIAL
                        {
                            Flags = 0,
                            Type = NativeMethods.CRED_TYPE_GENERIC,
                            TargetName = probeTarget,
                            CredentialBlobSize = 1,
                            CredentialBlob = blobPtr,
                            Persist = NativeMethods.CRED_PERSIST_SESSION
                        };

                        if (NativeMethods.CredWrite(ref cred, 0))
                        {
                            NativeMethods.CredDelete(probeTarget, NativeMethods.CRED_TYPE_GENERIC, 0);
                            _isAvailable = true;
                            return true;
                        }

                        _isAvailable = false;
                        return false;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(blobPtr);
                    }
                }
                catch
                {
                    _isAvailable = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CredentialManagerStorage"/> class.
        /// </summary>
        /// <param name="storageNamespace">Application-scoped namespace prefix (e.g. "Sops.Licensing").</param>
        public CredentialManagerStorage(string storageNamespace = "Sops.Licensing")
        {
            _namespace = string.IsNullOrWhiteSpace(storageNamespace) ? "Sops.Licensing" : storageNamespace.Trim();
        }

        private string BuildTargetName(string key) => $"{_namespace}:{key.Trim()}";

        /// <inheritdoc />
        public Task SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!IsAvailable())
            {
                throw new SecureStorageException("Windows Credential Manager is not accessible in the current logon session or sandbox.", ProviderName);
            }

            if (data.Length > MaxBlobSize)
            {
                throw new SecureStorageException(
                    $"Payload size ({data.Length} bytes) exceeds maximum Windows Credential Manager limit ({MaxBlobSize} bytes). Use DPAPI storage for larger data.",
                    ProviderName);
            }

            lock (_syncLock)
            {
                string target = BuildTargetName(key);

                // Check if already exists
                if (NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr existingCred))
                {
                    NativeMethods.CredFree(existingCred);
                    throw new SecureStorageException($"Credential '{key}' already exists. Use UpdateAsync to modify.", ProviderName);
                }

                WriteCredentialInternal(target, data);
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
            if (!IsAvailable()) return Task.FromResult<byte[]?>(null);

            lock (_syncLock)
            {
                string target = BuildTargetName(key);

                if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr credPtr))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == NativeMethods.ERROR_NOT_FOUND || err == NativeMethods.ERROR_FILE_NOT_FOUND || err == NativeMethods.ERROR_ACCESS_DENIED)
                    {
                        return Task.FromResult<byte[]?>(null);
                    }

                    throw new SecureStorageException($"CredRead failed with error code: {err}.", new Win32Exception(err), ProviderName);
                }

                try
                {
                    var credential = (NativeMethods.CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(NativeMethods.CREDENTIAL))!;
                    if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                    {
                        return Task.FromResult<byte[]?>(Array.Empty<byte>());
                    }

                    byte[] blob = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, blob, 0, credential.CredentialBlobSize);
                    return Task.FromResult<byte[]?>(blob);
                }
                finally
                {
                    NativeMethods.CredFree(credPtr);
                }
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

            if (!IsAvailable())
            {
                throw new SecureStorageException("Windows Credential Manager is not accessible in the current logon session or sandbox.", ProviderName);
            }

            if (data.Length > MaxBlobSize)
            {
                throw new SecureStorageException(
                    $"Payload size ({data.Length} bytes) exceeds maximum Windows Credential Manager limit ({MaxBlobSize} bytes).",
                    ProviderName);
            }

            lock (_syncLock)
            {
                string target = BuildTargetName(key);

                // Verify existence first
                if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr existingCred))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == NativeMethods.ERROR_NOT_FOUND || err == NativeMethods.ERROR_FILE_NOT_FOUND)
                    {
                        throw new SecureStorageException($"Credential '{key}' does not exist to update.", ProviderName);
                    }
                    throw new SecureStorageException($"Failed checking credential existence: error {err}.", new Win32Exception(err), ProviderName);
                }

                NativeMethods.CredFree(existingCred);
                WriteCredentialInternal(target, data);
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
            if (!IsAvailable()) return Task.FromResult(false);

            lock (_syncLock)
            {
                string target = BuildTargetName(key);
                if (NativeMethods.CredDelete(target, NativeMethods.CRED_TYPE_GENERIC, 0))
                {
                    return Task.FromResult(true);
                }

                int err = Marshal.GetLastWin32Error();
                if (err == NativeMethods.ERROR_NOT_FOUND || err == NativeMethods.ERROR_FILE_NOT_FOUND || err == NativeMethods.ERROR_ACCESS_DENIED)
                {
                    return Task.FromResult(false);
                }

                throw new SecureStorageException($"CredDelete failed with error code: {err}.", new Win32Exception(err), ProviderName);
            }
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) return Task.FromResult(false);
            if (!IsAvailable()) return Task.FromResult(false);

            lock (_syncLock)
            {
                string target = BuildTargetName(key);
                if (NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr credPtr))
                {
                    NativeMethods.CredFree(credPtr);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            var keys = new List<string>();
            if (!IsAvailable()) return Task.FromResult<IReadOnlyList<string>>(keys);
            lock (_syncLock)
            {
                string filter = $"{_namespace}:*";
                if (NativeMethods.CredEnumerate(filter, 0, out int count, out IntPtr credArrayPtr))
                {
                    try
                    {
                        int ptrSize = IntPtr.Size;
                        for (int i = 0; i < count; i++)
                        {
                            IntPtr credPtr = Marshal.ReadIntPtr(credArrayPtr, i * ptrSize);
                            if (credPtr != IntPtr.Zero)
                            {
                                var cred = (NativeMethods.CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(NativeMethods.CREDENTIAL))!;
                                if (!string.IsNullOrEmpty(cred.TargetName))
                                {
                                    string prefix = $"{_namespace}:";
                                    if (cred.TargetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                    {
                                        keys.Add(cred.TargetName.Substring(prefix.Length));
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        NativeMethods.CredFree(credArrayPtr);
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<string>>(keys);
        }

        private void WriteCredentialInternal(string target, byte[] data)
        {
            IntPtr blobPtr = IntPtr.Zero;
            try
            {
                blobPtr = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, blobPtr, data.Length);

                var cred = new NativeMethods.CREDENTIAL
                {
                    Flags = 0,
                    Type = NativeMethods.CRED_TYPE_GENERIC,
                    TargetName = target,
                    Comment = "SOPs Secure Storage Credential",
                    CredentialBlobSize = data.Length,
                    CredentialBlob = blobPtr,
                    Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                    UserName = Environment.UserName
                };

                if (!NativeMethods.CredWrite(ref cred, 0))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new SecureStorageException($"CredWrite failed with error code: {err}.", new Win32Exception(err), ProviderName);
                }
            }
            finally
            {
                if (blobPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(blobPtr);
                }
            }
        }

        private static class NativeMethods
        {
            public const int CRED_TYPE_GENERIC = 1;
            public const int CRED_PERSIST_SESSION = 1;
            public const int CRED_PERSIST_LOCAL_MACHINE = 2;
            public const int ERROR_FILE_NOT_FOUND = 2;
            public const int ERROR_ACCESS_DENIED = 5;
            public const int ERROR_NOT_FOUND = 1168;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CREDENTIAL
            {
                public int Flags;
                public int Type;
                public string TargetName;
                public string Comment;
                public long LastWritten;
                public int CredentialBlobSize;
                public IntPtr CredentialBlob;
                public int Persist;
                public int AttributeCount;
                public IntPtr Attributes;
                public string TargetAlias;
                public string UserName;
            }

            [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] int flags);

            [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredRead(string targetName, int type, int reservedFlag, out IntPtr credentialPtr);

            [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredDelete(string targetName, int type, int flags);

            [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredEnumerate(string filter, int flags, out int count, out IntPtr credentialPtr);

            [DllImport("Advapi32.dll", SetLastError = true)]
            public static extern void CredFree([In] IntPtr cred);
        }
    }
}
