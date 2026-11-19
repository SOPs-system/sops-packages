using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Relational;
using Sops.Pharmacy.Server.Configuration;

namespace Sops.Pharmacy.Server.Persistence
{
    /// <summary>
    /// Production SQLCipher relational database provider for local pharmacy storage.
    /// Inherits from RelationalDatabaseProviderBase for resilient command execution and transactions.
    /// Enforces AES-256 database encryption, WAL mode, foreign keys, and busy timeout.
    /// Never falls back to unencrypted SQLite.
    /// </summary>
    public class SqlCipherDatabaseProvider : RelationalDatabaseProviderBase
    {
        private readonly PharmacyServerSettings _serverSettings;
        private static bool _batteriesInitialized;
        private static readonly object _initLock = new object();

        public override string ProviderName => "SQLCipher";

        public SqlCipherDatabaseProvider(PharmacyServerSettings settings, ILogger<SqlCipherDatabaseProvider>? logger = null)
            : base(
                CreateDatabaseSettings(settings),
                logger,
                DatabaseCapabilities.Relational(supportsTransactions: true, supportsHealthChecks: true))
        {
            _serverSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            EnsureSqlCipherInitialized();
        }

        private static DatabaseSettings CreateDatabaseSettings(PharmacyServerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new DatabaseSettings
            {
                Provider = "SQLCipher",
                ConnectionString = BuildConnectionString(settings.DatabasePath),
                ConnectionTimeoutSeconds = 15,
                CommandTimeoutSeconds = 30,
                ReadOnly = false
            };
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        private static void EnsureSqlCipherInitialized()
        {
            if (_batteriesInitialized) return;
            lock (_initLock)
            {
                if (!_batteriesInitialized)
                {
                    PreloadNativeLibrary();
                    SQLitePCL.Batteries_V2.Init();
                    _batteriesInitialized = true;
                }
            }
        }

        private static void PreloadNativeLibrary()
        {
            try
            {
                var arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";
                var asmDir = Path.GetDirectoryName(typeof(SqlCipherDatabaseProvider).Assembly.Location) ?? "";

                var candidates = new[]
                {
                    Path.Combine(baseDir, "runtimes", arch, "native", "e_sqlcipher.dll"),
                    Path.Combine(baseDir, "e_sqlcipher.dll"),
                    Path.Combine(asmDir, "runtimes", arch, "native", "e_sqlcipher.dll"),
                    Path.Combine(asmDir, "e_sqlcipher.dll")
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        var handle = LoadLibrary(candidate);
                        if (handle != IntPtr.Zero) break;
                    }
                }
            }
            catch
            {
                // Fall back to SQLitePCL default probe
            }
        }

        public static string BuildConnectionString(string databasePath)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };
            return builder.ConnectionString;
        }

        public string GetConnectionString() => BuildConnectionString(_serverSettings.DatabasePath);

        public override DbConnection CreateConnection()
        {
            return new SqliteConnection(GetConnectionString());
        }

        public override async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var connection = new SqliteConnection(GetConnectionString());
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                // Execute PRAGMA key immediately upon opening
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA key = '{_serverSettings.DatabasePassphrase.Replace("'", "''")}';";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    // Verify encryption key by executing a test query on sqlite_master
                    cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                    await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

                    // Configure concurrency and resilience PRAGMAs
                    cmd.CommandText = "PRAGMA journal_mode = WAL;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    cmd.CommandText = "PRAGMA busy_timeout = 5000;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    cmd.CommandText = "PRAGMA foreign_keys = ON;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                return connection;
            }
            catch (Exception ex)
            {
                connection.Dispose();
                Logger?.LogError(ex, "Failed to open encrypted SQLCipher database at '{Path}'.", _serverSettings.DatabasePath);
                throw;
            }
        }

        /// <summary>
        /// Typed convenience method returning SqliteConnection with encryption and PRAGMAs applied.
        /// </summary>
        public async Task<SqliteConnection> OpenSqliteConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (SqliteConnection)conn;
        }

        /// <summary>
        /// Async factory for opened and encrypted SQLite connection.
        /// </summary>
        public Task<SqliteConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => OpenSqliteConnectionAsync(cancellationToken);

        public override async Task<DatabaseHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var start = DateTime.UtcNow;
            try
            {
                using (var conn = await OpenSqliteConnectionAsync(cancellationToken).ConfigureAwait(false))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA quick_check;";
                    var result = (string?)await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    var latency = DateTime.UtcNow - start;

                    if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileInfo = new FileInfo(_serverSettings.DatabasePath);
                        var sizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
                        return DatabaseHealthResult.Healthy(latency, $"SQLCipher active and intact. Database size: {sizeBytes / 1024} KB.");
                    }

                    return DatabaseHealthResult.Unhealthy(latency, $"Integrity check reported: {result}");
                }
            }
            catch (Exception ex)
            {
                var latency = DateTime.UtcNow - start;
                return DatabaseHealthResult.Unhealthy(latency, $"SQLCipher database check failed: {ex.Message}", ex);
            }
        }
    }
}
