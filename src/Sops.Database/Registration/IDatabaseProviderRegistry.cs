using System;
using System.Collections.Generic;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;

namespace Sops.Database.Registration
{
    /// <summary>
    /// Contract for managing and creating database providers dynamically from configuration settings.
    /// Avoids global mutable state by scoping registries to instances or dependency injection containers.
    /// </summary>
    public interface IDatabaseProviderRegistry
    {
        /// <summary>
        /// Gets the names of all currently registered database providers.
        /// </summary>
        IReadOnlyCollection<string> RegisteredProviders { get; }

        /// <summary>
        /// Registers a database provider factory with the specified identifier.
        /// </summary>
        /// <param name="providerName">The provider name (e.g., "PostgreSql", "Sqlite", "MongoDb").</param>
        /// <param name="factory">The factory delegate creating the provider instance.</param>
        void Register(string providerName, Func<DatabaseSettings, IServiceProvider?, IDatabaseProvider> factory);

        /// <summary>
        /// Checks whether a provider with the given name is registered.
        /// </summary>
        /// <param name="providerName">The provider identifier.</param>
        /// <returns><c>true</c> if registered; otherwise, <c>false</c>.</returns>
        bool IsRegistered(string providerName);

        /// <summary>
        /// Creates an instance of <see cref="IDatabaseProvider"/> for the provider specified in the settings.
        /// </summary>
        /// <param name="settings">The database configuration settings.</param>
        /// <param name="serviceProvider">Optional service provider for dependency resolution.</param>
        /// <returns>A configured <see cref="IDatabaseProvider"/> instance.</returns>
        IDatabaseProvider CreateProvider(DatabaseSettings settings, IServiceProvider? serviceProvider = null);
    }
}
