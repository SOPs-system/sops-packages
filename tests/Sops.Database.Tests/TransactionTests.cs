using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Relational;
using Sops.Database.Tests.TestHelpers;
using Xunit;

namespace Sops.Database.Tests
{
    public class TransactionTests : IDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;
        private readonly SqliteTestDatabaseProvider _provider;

        public TransactionTests()
        {
            string dbName = "tx_test_" + Guid.NewGuid().ToString("N");
            string connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();

            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = connectionString
            };

            _provider = new SqliteTestDatabaseProvider(settings);

            // Initialize table
            _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "CREATE TABLE Balances (Account TEXT PRIMARY KEY, Amount DECIMAL);")).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _keepAliveConnection.Dispose();
        }

        [Fact]
        public async Task Transaction_Commit_PersistsAllChanges()
        {
            using (IRelationalTransaction tx = await _provider.BeginTransactionAsync())
            {
                var cmd1 = RelationalCommandDefinition.Create(
                    "INSERT INTO Balances (Account, Amount) VALUES (@Account, @Amount);",
                    new { Account = "ACC-1", Amount = 100.0m },
                    transaction: tx.Transaction);

                var cmd2 = RelationalCommandDefinition.Create(
                    "INSERT INTO Balances (Account, Amount) VALUES (@Account, @Amount);",
                    new { Account = "ACC-2", Amount = 250.0m },
                    transaction: tx.Transaction);

                await _provider.ExecuteNonQueryAsync(cmd1);
                await _provider.ExecuteNonQueryAsync(cmd2);

                await tx.CommitAsync();
            }

            long? count = await _provider.ExecuteScalarAsync<long>(
                RelationalCommandDefinition.Idempotent("SELECT COUNT(*) FROM Balances WHERE Account IN ('ACC-1', 'ACC-2');"));

            Assert.Equal(2L, count);
        }

        [Fact]
        public async Task Transaction_Rollback_DiscardsAllChanges()
        {
            using (IRelationalTransaction tx = await _provider.BeginTransactionAsync())
            {
                var cmd = RelationalCommandDefinition.Create(
                    "INSERT INTO Balances (Account, Amount) VALUES (@Account, @Amount);",
                    new { Account = "ACC-ROLLBACK", Amount = 500.0m },
                    transaction: tx.Transaction);

                await _provider.ExecuteNonQueryAsync(cmd);
                await tx.RollbackAsync();
            }

            long? count = await _provider.ExecuteScalarAsync<long>(
                RelationalCommandDefinition.Idempotent("SELECT COUNT(*) FROM Balances WHERE Account = 'ACC-ROLLBACK';"));

            Assert.Equal(0L, count);
        }

        [Fact]
        public async Task Transaction_DisposeWithoutCommit_AutomaticallyRollsBack()
        {
            using (IRelationalTransaction tx = await _provider.BeginTransactionAsync())
            {
                var cmd = RelationalCommandDefinition.Create(
                    "INSERT INTO Balances (Account, Amount) VALUES (@Account, @Amount);",
                    new { Account = "ACC-DISPOSE-ROLLBACK", Amount = 999.0m },
                    transaction: tx.Transaction);

                await _provider.ExecuteNonQueryAsync(cmd);
                // Exit without calling CommitAsync()
            }

            long? count = await _provider.ExecuteScalarAsync<long>(
                RelationalCommandDefinition.Idempotent("SELECT COUNT(*) FROM Balances WHERE Account = 'ACC-DISPOSE-ROLLBACK';"));

            Assert.Equal(0L, count);
        }

        [Fact]
        public async Task Transaction_CommitTwice_ThrowsInvalidOperationException()
        {
            using (IRelationalTransaction tx = await _provider.BeginTransactionAsync())
            {
                await tx.CommitAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync());
            }
        }

        [Fact]
        public async Task ProviderWithoutTransactionCapability_ThrowsNotSupportedException()
        {
            var noTxCapabilities = DatabaseCapabilities.Relational(supportsTransactions: false);
            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = "Data Source=:memory:"
            };

            var provider = new SqliteTestDatabaseProvider(settings, capabilities: noTxCapabilities);

            await Assert.ThrowsAsync<NotSupportedException>(() => provider.BeginTransactionAsync());
        }
    }
}
