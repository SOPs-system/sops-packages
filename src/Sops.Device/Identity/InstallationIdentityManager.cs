using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;

namespace Sops.Device.Identity
{
    /// <summary>
    /// Manages the generation, retrieval, and thread-safe caching of the persistent installation identifier.
    /// </summary>
    public sealed class InstallationIdentityManager
    {
        private readonly IDeviceIdentityStore _store;
        private readonly ILogger<InstallationIdentityManager>? _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private string? _cachedInstallationId;

        public InstallationIdentityManager(IDeviceIdentityStore store, ILogger<InstallationIdentityManager>? logger = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger;
        }

        /// <summary>
        /// Gets the existing persisted installation ID, or creates, stores, and returns a new GUID on first launch.
        /// </summary>
        public async Task<string> GetOrCreateInstallationIdAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_cachedInstallationId))
            {
                return _cachedInstallationId!;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrEmpty(_cachedInstallationId))
                {
                    return _cachedInstallationId!;
                }

                // Try reading from store
                string? existingId = await _store.ReadInstallationIdAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(existingId) && Guid.TryParse(existingId, out _))
                {
                    _cachedInstallationId = existingId!.Trim().ToLowerInvariant();
                    return _cachedInstallationId;
                }

                // Generate new persistent GUID
                string newId = Guid.NewGuid().ToString("D").ToLowerInvariant();
                await _store.SaveInstallationIdAsync(newId, cancellationToken).ConfigureAwait(false);

                _logger?.LogInformation("Initialized new SOPs installation identity: {InstallationId}", newId);
                _cachedInstallationId = newId;
                return _cachedInstallationId;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
