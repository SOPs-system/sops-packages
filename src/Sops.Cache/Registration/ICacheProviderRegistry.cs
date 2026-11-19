using System;
using System.Collections.Generic;
using Sops.Cache.Abstractions;

namespace Sops.Cache.Registration
{
    /// <summary>
    /// Contract for managing and dynamically resolving cache providers without global mutable state.
    /// </summary>
    public interface ICacheProviderRegistry
    {
        /// <summary>
        /// Gets the names of all currently registered cache providers.
        /// </summary>
        IReadOnlyCollection<string> RegisteredProviders { get; }

        /// <summary>
        /// Registers a cache provider factory with the specified identifier.
        /// </summary>
        /// <param name="providerName">The provider identifier (e.g. "Memory", "Redis").</param>
        /// <param name="factory">The factory delegate creating the cache service.</param>
        void Register(string providerName, Func<IServiceProvider?, ICacheService> factory);

        /// <summary>
        /// Determines whether a provider with the given name has been registered.
        /// </summary>
        /// <param name="providerName">The provider identifier.</param>
        /// <returns><c>true</c> if registered; otherwise, <c>false</c>.</returns>
        bool IsRegistered(string providerName);

        /// <summary>
        /// Creates an instance of <see cref="ICacheService"/> for the specified provider name.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="serviceProvider">Optional service provider for dependency resolution.</param>
        /// <returns>A configured <see cref="ICacheService"/> instance.</returns>
        ICacheService CreateProvider(string providerName, IServiceProvider? serviceProvider = null);
    }
}
