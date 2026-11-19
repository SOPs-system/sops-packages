using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;

namespace Sops.Licensing.Storage.Windows
{
    /// <summary>
    /// Unified Windows secure storage combining Windows Credential Manager (for small secrets, keys, and tokens)
    /// and DPAPI file storage (for larger certificates and arbitrary payload data).
    /// </summary>
    public class HybridWindowsSecureStorage : ISecureStorage
    {
        private readonly CredentialManagerStorage _credManager;
        private readonly DpapiFileStorage _dpapi;

        /// <summary>Threshold size in bytes under which data is stored in Windows Credential Manager.</summary>
        public const int CredentialManagerThresholdBytes = 512;

        /// <inheritdoc />
        public string ProviderName => "WindowsHybrid(CredManager+DPAPI)";

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridWindowsSecureStorage"/> class.
        /// </summary>
        /// <param name="storageNamespace">Application storage namespace (default: "Sops.Licensing").</param>
        /// <param name="storageDirectory">Optional base directory for DPAPI storage.</param>
        public HybridWindowsSecureStorage(
            string storageNamespace = "Sops.Licensing",
            string? storageDirectory = null)
        {
            _credManager = new CredentialManagerStorage(storageNamespace);
            _dpapi = new DpapiFileStorage(storageDirectory, DpapiScope.CurrentUser, storageNamespace);
        }

        /// <inheritdoc />
        public async Task SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (await ExistsAsync(key, cancellationToken).ConfigureAwait(false))
            {
                throw new SecureStorageException($"Credential '{key}' already exists.", ProviderName);
            }

            if (data.Length <= CredentialManagerThresholdBytes && CredentialManagerStorage.IsAvailable())
            {
                await _credManager.SaveAsync(key, data, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _dpapi.SaveAsync(key, data, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public Task SaveStringAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return SaveAsync(key, Encoding.UTF8.GetBytes(value), cancellationToken);
        }

        /// <inheritdoc />
        public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));

            // Check Credential Manager first if available
            if (CredentialManagerStorage.IsAvailable())
            {
                byte[]? credData = await _credManager.GetAsync(key, cancellationToken).ConfigureAwait(false);
                if (credData != null)
                {
                    return credData;
                }
            }

            // Check DPAPI storage
            return await _dpapi.GetAsync(key, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            byte[]? data = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            return data != null ? Encoding.UTF8.GetString(data) : null;
        }

        /// <inheritdoc />
        public async Task UpdateAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            bool inCredManager = CredentialManagerStorage.IsAvailable() && await _credManager.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
            bool inDpapi = await _dpapi.ExistsAsync(key, cancellationToken).ConfigureAwait(false);

            if (!inCredManager && !inDpapi)
            {
                throw new SecureStorageException($"Credential '{key}' does not exist to update.", ProviderName);
            }

            // If updating small to large, migrate between backends
            if (data.Length <= CredentialManagerThresholdBytes && CredentialManagerStorage.IsAvailable())
            {
                if (inDpapi) await _dpapi.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

                if (inCredManager)
                {
                    await _credManager.UpdateAsync(key, data, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _credManager.SaveAsync(key, data, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                if (inCredManager) await _credManager.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

                if (inDpapi)
                {
                    await _dpapi.UpdateAsync(key, data, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _dpapi.SaveAsync(key, data, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public Task UpdateStringAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return UpdateAsync(key, Encoding.UTF8.GetBytes(value), cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            bool r1 = false;
            if (CredentialManagerStorage.IsAvailable())
            {
                r1 = await _credManager.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            }
            bool r2 = await _dpapi.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

            return r1 || r2;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (CredentialManagerStorage.IsAvailable() && await _credManager.ExistsAsync(key, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            return await _dpapi.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (CredentialManagerStorage.IsAvailable())
            {
                var k1 = await _credManager.ListKeysAsync(cancellationToken).ConfigureAwait(false);
                foreach (var k in k1) keys.Add(k);
            }

            var k2 = await _dpapi.ListKeysAsync(cancellationToken).ConfigureAwait(false);
            foreach (var k in k2) keys.Add(k);

            var list = new List<string>(keys);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }
    }
}
