using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Database.Relational
{
    /// <summary>
    /// Default implementation of <see cref="IRelationalTransaction"/> wrapping an ADO.NET <see cref="DbTransaction"/>.
    /// </summary>
    public sealed class RelationalTransaction : IRelationalTransaction
    {
        private readonly DbConnection? _connection;
        private readonly bool _ownsConnection;
        private int _completed; // 0 = active, 1 = committed/rolled back, 2 = disposed

        /// <inheritdoc />
        public DbTransaction Transaction { get; }

        /// <inheritdoc />
        public DbConnection Connection => _connection ?? Transaction.Connection ?? throw new InvalidOperationException("Transaction connection has already been closed or detached.");

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationalTransaction"/> class.
        /// </summary>
        /// <param name="transaction">The underlying database transaction.</param>
        /// <param name="ownsConnection">Whether this transaction wrapper should dispose the connection upon disposal.</param>
        public RelationalTransaction(DbTransaction transaction, bool ownsConnection = false)
        {
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _connection = transaction.Connection;
            _ownsConnection = ownsConnection;
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            {
                throw new InvalidOperationException("Transaction has already been completed or disposed.");
            }

            Transaction.Commit();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            {
                // If already completed or disposed, rollback is a safe no-op
                return Task.CompletedTask;
            }

            Transaction.Rollback();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _completed, 2) == 0)
            {
                try
                {
                    Transaction.Rollback();
                }
                catch
                {
                    // Suppress rollback failures during cleanup
                }
            }

            Transaction.Dispose();

            if (_ownsConnection && _connection != null)
            {
                _connection.Dispose();
            }
        }
    }
}
