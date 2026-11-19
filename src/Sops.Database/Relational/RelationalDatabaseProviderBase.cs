using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Resilience;

namespace Sops.Database.Relational
{
    /// <summary>
    /// Base class for relational database providers providing connection lifecycle, execution, transactions, resilience, and health probes.
    /// </summary>
    public abstract class RelationalDatabaseProviderBase : IRelationalDatabaseProvider
    {
        private readonly DatabaseRetryPolicy _retryPolicy;
        private bool _disposed;

        /// <summary>
        /// Gets the configuration settings associated with this provider.
        /// </summary>
        protected DatabaseSettings Settings { get; }

        /// <summary>
        /// Gets the optional logger for database diagnostics and events.
        /// </summary>
        protected ILogger? Logger { get; }

        /// <inheritdoc />
        public abstract string ProviderName { get; }

        /// <inheritdoc />
        public virtual DatabaseCapabilities Capabilities { get; } = DatabaseCapabilities.Relational();

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationalDatabaseProviderBase"/> class.
        /// </summary>
        /// <param name="settings">The database settings.</param>
        /// <param name="logger">Optional logger.</param>
        /// <param name="capabilities">Optional capability overrides.</param>
        protected RelationalDatabaseProviderBase(
            DatabaseSettings settings,
            ILogger? logger = null,
            DatabaseCapabilities? capabilities = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Logger = logger;
            _retryPolicy = DatabaseRetryPolicy.FromSettings(settings.Retry);

            if (capabilities != null)
            {
                Capabilities = capabilities;
            }

            Logger?.LogInformation(
                "Initialized database provider '{Provider}' (ReadOnly={ReadOnly}, Timeout={Timeout}s).",
                settings.Provider,
                settings.ReadOnly,
                settings.ConnectionTimeoutSeconds);
        }

        /// <inheritdoc />
        public abstract DbConnection CreateConnection();

        /// <summary>
        /// Gets the SQL command text used to perform lightweight health check probes.
        /// Defaults to "SELECT 1". Override if the target dialect requires a different syntax.
        /// </summary>
        /// <returns>A probe SQL query string.</returns>
        protected virtual string GetHealthProbeCommandText() => "SELECT 1;";

        /// <inheritdoc />
        public virtual async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await DatabaseRetryExecutor.ExecuteAsync(
                async ct =>
                {
                    DbConnection connection = CreateConnection();
                    try
                    {
                        if (connection.ConnectionTimeout != Settings.ConnectionTimeoutSeconds)
                        {
                            // Some providers do not permit changing timeout on connection object, ignore if unsupported
                        }

                        await connection.OpenAsync(ct).ConfigureAwait(false);
                        return connection;
                    }
                    catch (Exception ex)
                    {
                        connection.Dispose();
                        Logger?.LogError(
                            "Failed to open connection to provider '{Provider}'. Error: {ErrorType}: {ErrorMessage}",
                            ProviderName,
                            ex.GetType().Name,
                            ex.Message);
                        throw;
                    }
                },
                _retryPolicy,
                isIdempotent: true,
                logger: Logger,
                operationName: $"{ProviderName}.OpenConnection",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<IRelationalTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!Capabilities.SupportsTransactions)
            {
                throw new NotSupportedException($"Database provider '{ProviderName}' does not support transactions.");
            }

            DbConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                DbTransaction transaction = connection.BeginTransaction(isolationLevel);
                Logger?.LogDebug(
                    "Started transaction with isolation level '{IsolationLevel}' on provider '{Provider}'.",
                    isolationLevel,
                    ProviderName);

                return new RelationalTransaction(transaction, ownsConnection: true);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> ExecuteNonQueryAsync(
            RelationalCommandDefinition command,
            CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            ThrowIfDisposed();

            return await DatabaseRetryExecutor.ExecuteAsync(
                async ct =>
                {
                    if (command.Transaction != null)
                    {
                        using (DbCommand dbCommand = command.Transaction.Connection.CreateCommand())
                        {
                            command.ApplyTo(dbCommand);
                            return await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                        }
                    }

                    using (DbConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false))
                    using (DbCommand dbCommand = connection.CreateCommand())
                    {
                        command.ApplyTo(dbCommand);
                        return await dbCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                },
                _retryPolicy,
                isIdempotent: command.IsIdempotent,
                logger: Logger,
                operationName: $"{ProviderName}.ExecuteNonQuery",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<T?> ExecuteScalarAsync<T>(
            RelationalCommandDefinition command,
            CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            ThrowIfDisposed();

            return await DatabaseRetryExecutor.ExecuteAsync(
                async ct =>
                {
                    object? rawResult;

                    if (command.Transaction != null)
                    {
                        using (DbCommand dbCommand = command.Transaction.Connection.CreateCommand())
                        {
                            command.ApplyTo(dbCommand);
                            rawResult = await dbCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        using (DbConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false))
                        using (DbCommand dbCommand = connection.CreateCommand())
                        {
                            command.ApplyTo(dbCommand);
                            rawResult = await dbCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
                        }
                    }

                    if (rawResult == null || rawResult == DBNull.Value)
                    {
                        return default;
                    }

                    Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                    return (T)Convert.ChangeType(rawResult, targetType);
                },
                _retryPolicy,
                isIdempotent: command.IsIdempotent,
                logger: Logger,
                operationName: $"{ProviderName}.ExecuteScalar",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<IReadOnlyList<T>> QueryAsync<T>(
            RelationalCommandDefinition command,
            Func<DbDataReader, T> map,
            CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (map == null) throw new ArgumentNullException(nameof(map));
            ThrowIfDisposed();

            return await DatabaseRetryExecutor.ExecuteAsync(
                async ct =>
                {
                    var results = new List<T>();

                    if (command.Transaction != null)
                    {
                        using (DbCommand dbCommand = command.Transaction.Connection.CreateCommand())
                        {
                            command.ApplyTo(dbCommand);
                            using (DbDataReader reader = await dbCommand.ExecuteReaderAsync(ct).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                                {
                                    results.Add(map(reader));
                                }
                            }
                        }
                    }
                    else
                    {
                        using (DbConnection connection = await OpenConnectionAsync(ct).ConfigureAwait(false))
                        using (DbCommand dbCommand = connection.CreateCommand())
                        {
                            command.ApplyTo(dbCommand);
                            using (DbDataReader reader = await dbCommand.ExecuteReaderAsync(ct).ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                                {
                                    results.Add(map(reader));
                                }
                            }
                        }
                    }

                    return results;
                },
                _retryPolicy,
                isIdempotent: command.IsIdempotent,
                logger: Logger,
                operationName: $"{ProviderName}.Query",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<DatabaseHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!Capabilities.SupportsHealthChecks)
            {
                return DatabaseHealthResult.Healthy(TimeSpan.Zero, "Health checks not supported by provider.");
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using (DbConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
                using (DbCommand probeCommand = connection.CreateCommand())
                {
                    probeCommand.CommandText = GetHealthProbeCommandText();
                    probeCommand.CommandTimeout = Math.Min(Settings.CommandTimeoutSeconds, 5); // Short timeout for probe
                    await probeCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }

                stopwatch.Stop();
                Logger?.LogDebug("Health check for '{Provider}' succeeded in {ElapsedMs}ms.", ProviderName, stopwatch.ElapsedMilliseconds);
                return DatabaseHealthResult.Healthy(stopwatch.Elapsed, $"Database '{ProviderName}' is healthy.");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger?.LogError(
                    "Health check for '{Provider}' failed after {ElapsedMs}ms. Error: {ErrorType}: {ErrorMessage}",
                    ProviderName,
                    stopwatch.ElapsedMilliseconds,
                    ex.GetType().Name,
                    ex.Message);

                return DatabaseHealthResult.Unhealthy(
                    stopwatch.Elapsed,
                    $"Database health check failed for '{ProviderName}'.",
                    ex);
            }
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the provider has already been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName, $"Database provider '{ProviderName}' has been disposed.");
            }
        }

        /// <summary>
        /// Releases managed and unmanaged resources used by the database provider.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
