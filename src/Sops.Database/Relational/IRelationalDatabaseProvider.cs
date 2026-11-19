using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Sops.Database.Abstractions;

namespace Sops.Database.Relational
{
    /// <summary>
    /// Contract for relational database providers utilizing standard ADO.NET abstractions (<see cref="DbConnection"/>, <see cref="DbCommand"/>, <see cref="DbDataReader"/>).
    /// </summary>
    public interface IRelationalDatabaseProvider : IDatabaseProvider
    {
        /// <summary>
        /// Creates an unopened <see cref="DbConnection"/> configured with the provider's connection settings.
        /// </summary>
        /// <returns>A new <see cref="DbConnection"/> instance.</returns>
        DbConnection CreateConnection();

        /// <summary>
        /// Asynchronously creates and opens a new <see cref="DbConnection"/>.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the connection opening.</param>
        /// <returns>A task representing the asynchronous operation that yields an open connection.</returns>
        Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously opens a dedicated connection and begins an explicit database transaction.
        /// The caller must commit, rollback, or dispose the returned transaction.
        /// </summary>
        /// <param name="isolationLevel">The transaction isolation level. Defaults to <see cref="IsolationLevel.ReadCommitted"/>.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the active transaction wrapper.</returns>
        Task<IRelationalTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a parameterized relational command and returns the number of rows affected.
        /// Non-idempotent commands are never retried automatically on failure.
        /// </summary>
        /// <param name="command">The relational command definition.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> ExecuteNonQueryAsync(
            RelationalCommandDefinition command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a parameterized relational command and returns the scalar value from the first row and column.
        /// </summary>
        /// <typeparam name="T">The expected scalar value type.</typeparam>
        /// <param name="command">The relational command definition.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The scalar value converted to <typeparamref name="T"/>, or default.</returns>
        Task<T?> ExecuteScalarAsync<T>(
            RelationalCommandDefinition command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a query command and maps the resulting data reader stream to an immutable list of objects.
        /// </summary>
        /// <typeparam name="T">The model item type to produce.</typeparam>
        /// <param name="command">The relational command definition.</param>
        /// <param name="map">The mapper delegate translating each row of the <see cref="DbDataReader"/> to an instance of <typeparamref name="T"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A read-only list of mapped results.</returns>
        Task<IReadOnlyList<T>> QueryAsync<T>(
            RelationalCommandDefinition command,
            Func<DbDataReader, T> map,
            CancellationToken cancellationToken = default);
    }
}
