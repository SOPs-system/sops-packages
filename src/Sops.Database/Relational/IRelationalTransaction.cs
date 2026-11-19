using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Database.Relational
{
    /// <summary>
    /// Represents an active relational database transaction with asynchronous commit, rollback, and disposal.
    /// </summary>
    public interface IRelationalTransaction : IDisposable
    {
        /// <summary>
        /// Gets the underlying ADO.NET transaction instance.
        /// </summary>
        DbTransaction Transaction { get; }

        /// <summary>
        /// Gets the connection associated with this transaction.
        /// </summary>
        DbConnection Connection { get; }

        /// <summary>
        /// Asynchronously commits the database transaction.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the commit operation.</param>
        /// <returns>A task representing the asynchronous commit operation.</returns>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously rolls back the database transaction.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the rollback operation.</param>
        /// <returns>A task representing the asynchronous rollback operation.</returns>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
