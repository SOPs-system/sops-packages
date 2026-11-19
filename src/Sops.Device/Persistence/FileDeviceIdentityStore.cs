using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Models;

namespace Sops.Device.Persistence
{
    /// <summary>
    /// Production-quality, atomic file-based identity store with file corruption detection,
    /// cross-process file-level synchronization, retry with exponential backoff for sharing violations,
    /// and graceful handling of read-only environments.
    /// </summary>
    public sealed class FileDeviceIdentityStore : IDeviceIdentityStore
    {
        private readonly string _filePath;
        private readonly string _lockFilePath;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;
        private readonly ILogger<FileDeviceIdentityStore>? _logger;
        private readonly SemaphoreSlim _inProcessSemaphore = new SemaphoreSlim(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private sealed class StoredPayload
        {
            public int Version { get; set; } = 1;
            public string? Checksum { get; set; }
            public string? InstallationId { get; set; }
            public DeviceIdentityRecord? Identity { get; set; }
        }

        private sealed class DeviceIdentityRecord
        {
            public string InstallationId { get; set; } = string.Empty;
            public string DeviceCode { get; set; } = string.Empty;
            public int Mode { get; set; }
            public int Platform { get; set; }
            public DeviceFingerprintRecord? Fingerprint { get; set; }
            public DateTimeOffset CreatedAtUtc { get; set; }
            public DateTimeOffset LastValidatedAtUtc { get; set; }
        }

        private sealed class DeviceFingerprintRecord
        {
            public int FingerprintVersion { get; set; }
            public string Scope { get; set; } = "default";
            public string CombinedHash { get; set; } = string.Empty;
            public System.Collections.Generic.Dictionary<string, string> SignalHashes { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
            public System.Collections.Generic.List<string> IncludedSignals { get; set; } = new System.Collections.Generic.List<string>();
            public DateTimeOffset GeneratedAtUtc { get; set; }
        }

        public FileDeviceIdentityStore(
            string? customFilePath = null,
            int maxRetries = 3,
            int retryDelayMs = 100,
            ILogger<FileDeviceIdentityStore>? logger = null)
        {
            _logger = logger;
            _maxRetries = Math.Max(1, maxRetries);
            _retryDelayMs = Math.Max(10, retryDelayMs);

            if (!string.IsNullOrWhiteSpace(customFilePath))
            {
                _filePath = customFilePath!;
            }
            else
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                }
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = Path.GetTempPath();
                }

                _filePath = Path.Combine(baseDir, "SOPs", "Device", "identity.json");
            }

            _lockFilePath = _filePath + ".lock";
        }

        /// <inheritdoc />
        public async Task<string?> ReadInstallationIdAsync(CancellationToken cancellationToken = default)
        {
            var payload = await ReadPayloadAsync(cancellationToken).ConfigureAwait(false);
            return payload?.InstallationId;
        }

        /// <inheritdoc />
        public async Task SaveInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(installationId)) throw new ArgumentException("InstallationId cannot be empty.", nameof(installationId));

            await _inProcessSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false))
                {
                    var payload = await ReadPayloadInternalAsync().ConfigureAwait(false) ?? new StoredPayload();
                    payload.InstallationId = installationId.Trim();
                    await WritePayloadInternalAsync(payload).ConfigureAwait(false);
                }
            }
            finally
            {
                _inProcessSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<DeviceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken = default)
        {
            var payload = await ReadPayloadAsync(cancellationToken).ConfigureAwait(false);
            if (payload?.Identity == null) return null;

            var rec = payload.Identity;
            DeviceFingerprint? fp = null;
            if (rec.Fingerprint != null)
            {
                fp = new DeviceFingerprint(
                    rec.Fingerprint.CombinedHash,
                    rec.Fingerprint.SignalHashes,
                    rec.Fingerprint.IncludedSignals,
                    rec.Fingerprint.FingerprintVersion,
                    rec.Fingerprint.Scope,
                    rec.Fingerprint.GeneratedAtUtc);
            }

            return new DeviceIdentity(
                rec.InstallationId,
                rec.DeviceCode,
                (DeviceIdentityMode)rec.Mode,
                (DevicePlatform)rec.Platform,
                fp,
                rec.CreatedAtUtc,
                rec.LastValidatedAtUtc);
        }

        /// <inheritdoc />
        public async Task SaveIdentityAsync(DeviceIdentity identity, CancellationToken cancellationToken = default)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));

            await _inProcessSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false))
                {
                    var payload = await ReadPayloadInternalAsync().ConfigureAwait(false) ?? new StoredPayload();
                    payload.InstallationId = identity.InstallationId;

                    var rec = new DeviceIdentityRecord
                    {
                        InstallationId = identity.InstallationId,
                        DeviceCode = identity.DeviceCode,
                        Mode = (int)identity.Mode,
                        Platform = (int)identity.Platform,
                        CreatedAtUtc = identity.CreatedAtUtc,
                        LastValidatedAtUtc = identity.LastValidatedAtUtc
                    };

                    if (identity.Fingerprint != null)
                    {
                        var sigDict = new System.Collections.Generic.Dictionary<string, string>();
                        foreach (var kvp in identity.Fingerprint.SignalHashes)
                        {
                            sigDict[kvp.Key] = kvp.Value;
                        }

                        rec.Fingerprint = new DeviceFingerprintRecord
                        {
                            FingerprintVersion = identity.Fingerprint.FingerprintVersion,
                            Scope = identity.Fingerprint.Scope,
                            CombinedHash = identity.Fingerprint.CombinedHash,
                            SignalHashes = sigDict,
                            IncludedSignals = new System.Collections.Generic.List<string>(identity.Fingerprint.IncludedSignals),
                            GeneratedAtUtc = identity.Fingerprint.GeneratedAtUtc
                        };
                    }

                    payload.Identity = rec;
                    await WritePayloadInternalAsync(payload).ConfigureAwait(false);
                }
            }
            finally
            {
                _inProcessSemaphore.Release();
            }
        }

        private async Task<StoredPayload?> ReadPayloadAsync(CancellationToken cancellationToken)
        {
            await _inProcessSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false))
                {
                    return await ReadPayloadInternalAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _inProcessSemaphore.Release();
            }
        }

        private async Task<StoredPayload?> ReadPayloadInternalAsync()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            string? json = null;
            int attempt = 0;

            while (attempt < _maxRetries)
            {
                attempt++;
                try
                {
                    using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        json = await reader.ReadToEndAsync().ConfigureAwait(false);
                        break;
                    }
                }
                catch (IOException ex) when (attempt < _maxRetries)
                {
                    _logger?.LogDebug(ex, "Storage read contention on {Path}, retrying (attempt {Attempt}/{Max})", _filePath, attempt, _maxRetries);
                    await Task.Delay(_retryDelayMs * attempt).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger?.LogError(ex, "Permission denied accessing device identity at {Path}", _filePath);
                    throw new DeviceStorageException($"Permission denied accessing device identity storage at '{_filePath}'.", ex, _filePath);
                }
                catch (Exception ex) when (attempt >= _maxRetries)
                {
                    _logger?.LogError(ex, "Failed to read identity file at {Path} after {Max} attempts", _filePath, _maxRetries);
                    throw new DeviceStorageException($"Failed to access existing identity storage at '{_filePath}' due to I/O contention.", ex, _filePath);
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<StoredPayload>(json!, JsonOptions);
                if (payload == null) return null;

                // Verify corruption checksum
                if (!string.IsNullOrEmpty(payload.Checksum))
                {
                    string computedChecksum = ComputePayloadChecksum(payload.InstallationId, payload.Identity);
                    if (!string.Equals(payload.Checksum, computedChecksum, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogError("Corruption detected in device identity file {Path}. Checksum mismatch.", _filePath);
                        HandleCorruptedFile();
                        return null;
                    }
                }

                return payload;
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "JSON parsing error in device identity file {Path}", _filePath);
                HandleCorruptedFile();
                return null;
            }
        }

        private async Task WritePayloadInternalAsync(StoredPayload payload)
        {
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Compute and assign integrity checksum
                payload.Checksum = ComputePayloadChecksum(payload.InstallationId, payload.Identity);
                string json = JsonSerializer.Serialize(payload, JsonOptions);

                // Atomic write via temporary file
                string tempFile = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }

                // Atomic replace
                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Replace(tempFile, _filePath, null);
                    }
                    catch
                    {
                        File.Delete(_filePath);
                        File.Move(tempFile, _filePath);
                    }
                }
                else
                {
                    File.Move(tempFile, _filePath);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Read-only or restricted filesystem prevented saving identity to {Path}", _filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to atomically save device identity to {Path}", _filePath);
            }
        }

        private void HandleCorruptedFile()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string backupFile = _filePath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Move(_filePath, backupFile);
                    _logger?.LogWarning("Preserved corrupted identity file as {BackupPath}", backupFile);
                }
            }
            catch { }
        }

        private async Task<IDisposable> AcquireCrossProcessLockAsync(CancellationToken cancellationToken)
        {
            string? dir = Path.GetDirectoryName(_lockFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }

            int attempts = 0;
            while (attempts < _maxRetries * 3)
            {
                attempts++;
                try
                {
                    var lockStream = new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    return lockStream;
                }
                catch (IOException)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    await Task.Delay(_retryDelayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    // If lock directory is read-only or restricted, fallback to in-process locking
                    break;
                }
            }

            return new NullDisposable();
        }

        private static string ComputePayloadChecksum(string? installationId, DeviceIdentityRecord? identity)
        {
            var sb = new StringBuilder();
            sb.Append(installationId ?? string.Empty).Append('|');
            if (identity != null)
            {
                sb.Append(identity.InstallationId).Append('|')
                  .Append(identity.DeviceCode).Append('|')
                  .Append(identity.Mode).Append('|')
                  .Append(identity.Platform).Append('|');
                if (identity.Fingerprint != null)
                {
                    sb.Append(identity.Fingerprint.CombinedHash);
                }
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
        }

        private sealed class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
