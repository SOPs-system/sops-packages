using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Licensing.Abstractions
{
    /// <summary>
    /// Operating-system-backed secure storage contract for sensitive credentials, keys, and tokens.
    /// Provides application-scoped namespaces, binary and string storage, and atomic operations.
    /// </summary>
    public interface ISecureStorage
    {
        /// <summary>
        /// Gets the human-readable identifier of the underlying security provider (e.g. "WindowsCredentialManager", "WindowsDpapi", "MacKeychain", "LinuxSecretService", "LinuxEncryptedVault").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Securely saves binary data under the specified key within the current application namespace.
        /// Throws if the key already exists (use <see cref="UpdateAsync"/> or overwrite).
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="data">The secret byte array to store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience method to securely save string data under the specified key.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="value">The secret string value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveStringAsync(string key, string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the secret binary data associated with the specified key, or <c>null</c> if not found.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The decrypted secret byte array, or <c>null</c> if the key does not exist.</returns>
        Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience method to retrieve the secret string associated with the specified key.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The decrypted secret string, or <c>null</c> if not found.</returns>
        Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Securely updates existing binary data under the specified key. Throws if the key does not exist.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="data">The new secret byte array.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateAsync(string key, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience method to securely update existing string data under the specified key.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="value">The new secret string value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateStringAsync(string key, string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Securely deletes the secret associated with the specified key.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the item was found and deleted; otherwise, <c>false</c>.</returns>
        Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a credential with the specified key exists in secure storage.
        /// </summary>
        /// <param name="key">The credential key name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all credential keys stored within the current application-scoped namespace.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A read-only list of keys.</returns>
        Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default);
    }
}
