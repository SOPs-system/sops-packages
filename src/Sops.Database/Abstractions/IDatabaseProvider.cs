using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Database.Abstractions
{
    /// <summary>
    /// Core abstraction for all database providers (relational and non-relational).
    /// Provides provider identification, capability inspection, lifecycle management, and health checks.
    /// </summary>
    public interface IDatabaseProvider : IDisposable
    {
        /// <summary>
        /// Gets the unique name of the database provider (e.g., "PostgreSql", "SqlServer", "Sqlite", "MongoDb").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Gets the operational capabilities supported by this provider instance.
        /// </summary>
        DatabaseCapabilities Capabilities { get; }

        /// <summary>
        /// Executes an active health probe against the database to confirm connectivity and responsiveness.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the probe.</param>
        /// <returns>A task representing the asynchronous health check result.</returns>
        Task<DatabaseHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}
