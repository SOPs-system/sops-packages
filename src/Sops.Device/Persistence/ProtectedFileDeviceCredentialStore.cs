using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Management;

namespace Sops.Device.Persistence
{
    /// <summary>
    /// DPAPI protection scope on Windows systems.
    /// </summary>
    public enum DpapiProtectionScope
    {
        /// <summary>
        /// Protected data is associated with the current Windows user account.
        /// Only threads running under the same user credential can unprotect the data.
        /// Prevents other local users on the same machine from accessing credentials.
        /// </summary>
        CurrentUser = 0,

        /// <summary>
        /// Protected data is associated with the machine context. Any process on the computer can unprotect data.
        /// Suitable for workstation-wide POS terminals where multiple local Windows operator accounts share the physical register.
        /// </summary>
        LocalMachine = 1
    }

    /// <summary>
    /// Production-grade cross-platform secure store for sensitive device credentials.
    /// Utilizes native Windows DPAPI (CryptProtectData) on Windows and authenticated AES-256-CBC + HMAC-SHA256 on other platforms.
    /// Implements atomic file swaps, cross-process locking, and explicit credential loss detection.
    /// </summary>
    public sealed class ProtectedFileDeviceCredentialStore : IDeviceCredentialStore
    {
        private readonly string _filePath;
        private readonly string _lockFilePath;
        private readonly byte[]? _optionalEntropy;
        private readonly DpapiProtectionScope _protectionScope;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;
        private readonly ILogger<ProtectedFileDeviceCredentialStore>? _logger;
        private readonly object _syncLock = new object();

        /// <summary>The DPAPI protection scope applied on Windows platforms.</summary>
        public DpapiProtectionScope ProtectionScope => _protectionScope;

        public ProtectedFileDeviceCredentialStore(
            string? filePath = null,
            byte[]? optionalEntropy = null,
            DpapiProtectionScope protectionScope = DpapiProtectionScope.CurrentUser,
            int maxRetries = 3,
            int retryDelayMs = 100,
            ILogger<ProtectedFileDeviceCredentialStore>? logger = null)
        {
            _filePath = !string.IsNullOrWhiteSpace(filePath)
                ? filePath!
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SOPs",
                    "Device",
                    "device_credentials.dat");

            _lockFilePath = _filePath + ".lock";
            _optionalEntropy = optionalEntropy;
            _protectionScope = protectionScope;
            _maxRetries = Math.Max(1, maxRetries);
            _retryDelayMs = Math.Max(10, retryDelayMs);
            _logger = logger;

            EnsureDirectoryExists();
        }

        /// <inheritdoc />
        public async Task<LocalDeviceRegistration?> ReadRegistrationAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lock (_syncLock)
                    {
                        using (AcquireCrossProcessLock())
                        {
                            if (!File.Exists(_filePath)) return null;

                            byte[] encryptedBytes = File.ReadAllBytes(_filePath);
                            if (encryptedBytes == null || encryptedBytes.Length == 0)
                            {
                                throw new DeviceCredentialStorageException("Credential file is zero-length or empty.", isCredentialLost: true);
                            }

                            byte[] plainBytes = UnprotectData(encryptedBytes);
                            string json = Encoding.UTF8.GetString(plainBytes);

                            var payload = JsonSerializer.Deserialize<StoredCredentialPayload>(json);
                            if (payload == null)
                            {
                                throw new DeviceCredentialStorageException("Deserialized credential payload was null.", isCredentialLost: true);
                            }

                            return new LocalDeviceRegistration(
                                installationId: payload.InstallationId,
                                deviceCode: payload.DeviceCode,
                                registrationId: payload.RegistrationId,
                                lastKnownStatus: payload.LastKnownStatus,
                                serverRegistrationToken: payload.ServerRegistrationToken,
                                lastSyncAtUtc: payload.LastSyncAtUtc,
                                cachedAtUtc: payload.CachedAtUtc,
                                tenantId: payload.TenantId,
                                branchId: payload.BranchId);
                        }
                    }
                }
                catch (DeviceCredentialStorageException)
                {
                    // Re-throw permanent credential loss exceptions directly without retrying lock
                    throw;
                }
                catch (IOException ex) when (attempt < _maxRetries)
                {
                    _logger?.LogDebug(ex, "Transient I/O collision reading device credentials (attempt {Attempt}/{Max})", attempt, _maxRetries);
                    await Task.Delay(_retryDelayMs * attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to read or decrypt device credentials from {Path}", _filePath);
                    throw new DeviceCredentialStorageException($"Unable to decrypt device credentials at '{_filePath}': {ex.Message}", ex, isCredentialLost: true);
                }
            }

            throw new DeviceCredentialStorageException($"Unable to access credential file at '{_filePath}' due to persistent lock contention.", isCredentialLost: false);
        }

        /// <inheritdoc />
        public async Task SaveRegistrationAsync(LocalDeviceRegistration registration, CancellationToken cancellationToken = default)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));

            EnsureDirectoryExists();

            var payload = new StoredCredentialPayload
            {
                InstallationId = registration.InstallationId,
                DeviceCode = registration.DeviceCode,
                RegistrationId = registration.RegistrationId,
                LastKnownStatus = registration.LastKnownStatus,
                ServerRegistrationToken = registration.ServerRegistrationToken,
                LastSyncAtUtc = registration.LastSyncAtUtc,
                CachedAtUtc = registration.CachedAtUtc,
                TenantId = registration.TenantId,
                BranchId = registration.BranchId
            };

            string json = JsonSerializer.Serialize(payload);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = ProtectData(plainBytes);

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lock (_syncLock)
                    {
                        using (AcquireCrossProcessLock())
                        {
                            string tempFile = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            try
                            {
                                File.WriteAllBytes(tempFile, encryptedBytes);

                                if (File.Exists(_filePath))
                                {
                                    File.Replace(tempFile, _filePath, null);
                                }
                                else
                                {
                                    File.Move(tempFile, _filePath);
                                }
                                return;
                            }
                            finally
                            {
                                if (File.Exists(tempFile))
                                {
                                    try { File.Delete(tempFile); } catch { }
                                }
                            }
                        }
                    }
                }
                catch (IOException ex) when (attempt < _maxRetries)
                {
                    _logger?.LogDebug(ex, "Transient I/O collision writing device credentials (attempt {Attempt}/{Max})", attempt, _maxRetries);
                    await Task.Delay(_retryDelayMs * attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed writing device credentials to {Path}", _filePath);
                    throw new DeviceCredentialStorageException($"Failed to write credentials to '{_filePath}': {ex.Message}", ex, isCredentialLost: false);
                }
            }

            throw new DeviceCredentialStorageException($"Exhausted retries attempting to write credentials to '{_filePath}' due to lock contention.", isCredentialLost: false);
        }

        /// <inheritdoc />
        public Task ClearRegistrationAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                using (AcquireCrossProcessLock())
                {
                    if (File.Exists(_filePath))
                    {
                        try { File.Delete(_filePath); } catch { }
                    }
                }
            }
            return Task.CompletedTask;
        }

        private byte[] ProtectData(byte[] plainData)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DpapiProtect(plainData);
            }

            // Cross-platform fallback: AES-256 CBC with application entropy
            return AesFallbackProtect(plainData);
        }

        private byte[] UnprotectData(byte[] cipherData)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DpapiUnprotect(cipherData);
            }

            return AesFallbackUnprotect(cipherData);
        }

        private byte[] DpapiProtect(byte[] data)
        {
            var inBlob = new NativeMethods.DATA_BLOB
            {
                cbData = data.Length,
                pbData = Marshal.AllocHGlobal(data.Length)
            };
            Marshal.Copy(data, 0, inBlob.pbData, data.Length);

            IntPtr pEntropyBlob = IntPtr.Zero;
            IntPtr pEntropyBytes = IntPtr.Zero;
            var outBlob = new NativeMethods.DATA_BLOB();

            try
            {
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

                int flags = NativeMethods.CRYPTPROTECT_UI_FORBIDDEN;
                if (_protectionScope == DpapiProtectionScope.LocalMachine)
                {
                    flags |= NativeMethods.CRYPTPROTECT_LOCAL_MACHINE;
                }

                bool success = NativeMethods.CryptProtectData(
                    ref inBlob,
                    "SOPs.Device.Credentials",
                    pEntropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    flags,
                    out outBlob);

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"CryptProtectData failed with error code: {err}.");
                }

                byte[] cipher = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, cipher, 0, outBlob.cbData);
                return cipher;
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
                if (pEntropyBytes != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBytes);
                if (pEntropyBlob != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBlob);
                if (outBlob.pbData != IntPtr.Zero) NativeMethods.LocalFree(outBlob.pbData);
            }
        }

        private byte[] DpapiUnprotect(byte[] cipherData)
        {
            var inBlob = new NativeMethods.DATA_BLOB
            {
                cbData = cipherData.Length,
                pbData = Marshal.AllocHGlobal(cipherData.Length)
            };
            Marshal.Copy(cipherData, 0, inBlob.pbData, cipherData.Length);

            IntPtr pEntropyBlob = IntPtr.Zero;
            IntPtr pEntropyBytes = IntPtr.Zero;
            var outBlob = new NativeMethods.DATA_BLOB();

            try
            {
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

                int flags = NativeMethods.CRYPTPROTECT_UI_FORBIDDEN;
                if (_protectionScope == DpapiProtectionScope.LocalMachine)
                {
                    flags |= NativeMethods.CRYPTPROTECT_LOCAL_MACHINE;
                }

                bool success = NativeMethods.CryptUnprotectData(
                    ref inBlob,
                    IntPtr.Zero,
                    pEntropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    flags,
                    out outBlob);

                if (!success)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, $"CryptUnprotectData failed with error code: {err}.");
                }

                byte[] plain = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, plain, 0, outBlob.cbData);
                return plain;
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
                if (pEntropyBytes != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBytes);
                if (pEntropyBlob != IntPtr.Zero) Marshal.FreeHGlobal(pEntropyBlob);
                if (outBlob.pbData != IntPtr.Zero) NativeMethods.LocalFree(outBlob.pbData);
            }
        }

        private byte[] AesFallbackProtect(byte[] plainData)
        {
            if (_optionalEntropy == null || _optionalEntropy.Length == 0)
            {
                throw new DeviceCredentialStorageException(
                    "Secure key provisioning is required on non-Windows platforms. " +
                    "Supply an explicit non-empty master key or entropy to ProtectedFileDeviceCredentialStore.",
                    isCredentialLost: true);
            }

            // Derive independent encryption and authentication keys using distinct prefixes
            DeriveFallbackKeys(_optionalEntropy, out byte[] encKey, out byte[] macKey);

            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            byte[] encrypted;
            using (var aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.IV = iv;
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(plainData, 0, plainData.Length);
                        cs.FlushFinalBlock();
                    }
                    encrypted = ms.ToArray();
                }
            }

            // Encrypt-then-MAC: Compute HMAC over IV + Encrypted Data
            byte[] payload = new byte[iv.Length + encrypted.Length];
            Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
            Buffer.BlockCopy(encrypted, 0, payload, iv.Length, encrypted.Length);

            byte[] hmac;
            using (var hmacSha = new HMACSHA256(macKey))
            {
                hmac = hmacSha.ComputeHash(payload);
            }

            // Output format: [16-byte IV][Ciphertext][32-byte HMAC]
            byte[] result = new byte[payload.Length + hmac.Length];
            Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
            Buffer.BlockCopy(hmac, 0, result, payload.Length, hmac.Length);
            return result;
        }

        private byte[] AesFallbackUnprotect(byte[] cipherData)
        {
            if (_optionalEntropy == null || _optionalEntropy.Length == 0)
            {
                throw new DeviceCredentialStorageException(
                    "Secure key provisioning is required on non-Windows platforms. " +
                    "Supply an explicit non-empty master key or entropy to ProtectedFileDeviceCredentialStore.",
                    isCredentialLost: true);
            }

            // Payload must have at least 16 (IV) + 16 (AES block) + 32 (HMAC) = 64 bytes
            if (cipherData == null || cipherData.Length < 64)
            {
                throw new DeviceCredentialStorageException("Ciphertext payload is truncated or corrupted.", isCredentialLost: true);
            }

            DeriveFallbackKeys(_optionalEntropy, out byte[] encKey, out byte[] macKey);

            int payloadLen = cipherData.Length - 32;
            byte[] storedHmac = new byte[32];
            Buffer.BlockCopy(cipherData, payloadLen, storedHmac, 0, 32);

            // Verify MAC BEFORE decryption to prevent padding oracle and tampering
            byte[] computedHmac;
            using (var hmacSha = new HMACSHA256(macKey))
            {
                computedHmac = hmacSha.ComputeHash(cipherData, 0, payloadLen);
            }

            if (!FixedTimeEquals(storedHmac, computedHmac))
            {
                throw new DeviceCredentialStorageException(
                    "Ciphertext authentication failed. Data has been tampered with or master key is incorrect.",
                    isCredentialLost: true);
            }

            // Decrypt verified ciphertext
            byte[] iv = new byte[16];
            Buffer.BlockCopy(cipherData, 0, iv, 0, 16);

            int cipherLen = payloadLen - 16;
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = encKey;
                    aes.IV = iv;
                    using (var ms = new MemoryStream(cipherData, 16, cipherLen))
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    using (var outMs = new MemoryStream())
                    {
                        byte[] buffer = new byte[1024];
                        int read;
                        while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outMs.Write(buffer, 0, read);
                        }
                        return outMs.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DeviceCredentialStorageException($"AES decryption failed: {ex.Message}", ex, isCredentialLost: true);
            }
        }

        private static void DeriveFallbackKeys(byte[] masterKey, out byte[] encKey, out byte[] macKey)
        {
            using (var sha = SHA256.Create())
            {
                byte[] encSeed = new byte[masterKey.Length + 4];
                Buffer.BlockCopy(masterKey, 0, encSeed, 0, masterKey.Length);
                encSeed[masterKey.Length] = (byte)'e';
                encSeed[masterKey.Length + 1] = (byte)'n';
                encSeed[masterKey.Length + 2] = (byte)'c';
                encSeed[masterKey.Length + 3] = 1;
                encKey = sha.ComputeHash(encSeed);

                byte[] macSeed = new byte[masterKey.Length + 4];
                Buffer.BlockCopy(masterKey, 0, macSeed, 0, masterKey.Length);
                macSeed[masterKey.Length] = (byte)'m';
                macSeed[masterKey.Length + 1] = (byte)'a';
                macSeed[masterKey.Length + 2] = (byte)'c';
                macSeed[masterKey.Length + 3] = 2;
                macKey = sha.ComputeHash(macSeed);
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private IDisposable AcquireCrossProcessLock()
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    return new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (attempt < _maxRetries)
                {
                    Thread.Sleep(_retryDelayMs * attempt);
                }
            }

            return new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private void EnsureDirectoryExists()
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }
        }

        private sealed class StoredCredentialPayload
        {
            public string InstallationId { get; set; } = string.Empty;
            public string DeviceCode { get; set; } = string.Empty;
            public string? RegistrationId { get; set; }
            public DeviceStatus LastKnownStatus { get; set; }
            public string? ServerRegistrationToken { get; set; }
            public DateTimeOffset? LastSyncAtUtc { get; set; }
            public DateTimeOffset CachedAtUtc { get; set; }
            public string? TenantId { get; set; }
            public string? BranchId { get; set; }
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

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr LocalFree(IntPtr hMem);
        }
    }
}
