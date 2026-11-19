using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Relational;
using Sops.Database.Tests.TestHelpers;
using Xunit;

namespace Sops.Database.Tests
{
    public class RelationalContractTests : IDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;
        private readonly SqliteTestDatabaseProvider _provider;

        public RelationalContractTests()
        {
            string dbName = "relational_test_" + Guid.NewGuid().ToString("N");
            string connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

            // In SQLite in-memory shared cache, one open connection keeps the DB alive across other connections
            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();

            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = connectionString,
                CommandTimeoutSeconds = 10,
                ConnectionTimeoutSeconds = 10
            };

            _provider = new SqliteTestDatabaseProvider(settings);
        }

        public void Dispose()
        {
            _provider.Dispose();
            _keepAliveConnection.Dispose();
        }

        [Fact]
        public async Task OpenConnectionAsync_OpensConnectionSuccessfully()
        {
            using DbConnection conn = await _provider.OpenConnectionAsync();

            Assert.NotNull(conn);
            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }

        [Fact]
        public async Task ExecuteNonQueryAsync_CreatesTableAndInsertsData()
        {
            var createTable = RelationalCommandDefinition.Create(
                "CREATE TABLE Users (Id TEXT PRIMARY KEY, Name TEXT, Email TEXT);");

            int createResult = await _provider.ExecuteNonQueryAsync(createTable);
            Assert.Equal(0, createResult);

            var insertCmd = RelationalCommandDefinition.Create(
                "INSERT INTO Users (Id, Name, Email) VALUES (@Id, @Name, @Email);",
                new { Id = "u1", Name = "Alice", Email = "alice@example.com" });

            int insertResult = await _provider.ExecuteNonQueryAsync(insertCmd);
            Assert.Equal(1, insertResult);
        }

        [Fact]
        public async Task ExecuteScalarAsync_ReturnsTypedValue()
        {
            await _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "CREATE TABLE Products (Id INT PRIMARY KEY, Price DECIMAL);"));

            await _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "INSERT INTO Products (Id, Price) VALUES (1, 49.99);"));

            var countCmd = RelationalCommandDefinition.Idempotent("SELECT COUNT(*) FROM Products;");
            long? count = await _provider.ExecuteScalarAsync<long>(countCmd);

            Assert.Equal(1L, count);

            var priceCmd = RelationalCommandDefinition.Idempotent(
                "SELECT Price FROM Products WHERE Id = @Id;",
                new { Id = 1 });

            decimal? price = await _provider.ExecuteScalarAsync<decimal>(priceCmd);
            Assert.Equal(49.99m, price);
        }

        [Fact]
        public async Task QueryAsync_MapsResultSetToObjects()
        {
            await _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "CREATE TABLE Items (Code TEXT, Quantity INT);"));

            await _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "INSERT INTO Items (Code, Quantity) VALUES ('A1', 10), ('B2', 25), ('C3', 5);"));

            var queryCmd = RelationalCommandDefinition.Idempotent(
                "SELECT Code, Quantity FROM Items WHERE Quantity >= @MinQty ORDER BY Quantity ASC;",
                new { MinQty = 10 });

            IReadOnlyList<(string Code, int Qty)> items = await _provider.QueryAsync(
                queryCmd,
                reader => (reader.GetString(0), reader.GetInt32(1)));

            Assert.Equal(2, items.Count);
            Assert.Equal("A1", items[0].Code);
            Assert.Equal(10, items[0].Qty);
            Assert.Equal("B2", items[1].Code);
            Assert.Equal(25, items[1].Qty);
        }

        [Fact]
        public async Task RelationalCommandDefinition_HandlesNullParametersAsDbNull()
        {
            await _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "CREATE TABLE NullTest (Id TEXT PRIMARY KEY, Description TEXT NULL);"));

            var insertCmd = RelationalCommandDefinition.Create(
                "INSERT INTO NullTest (Id, Description) VALUES (@Id, @Description);",
                new Dictionary<string, object?>
                {
                    ["Id"] = "n1",
                    ["Description"] = null
                });

            int rows = await _provider.ExecuteNonQueryAsync(insertCmd);
            Assert.Equal(1, rows);

            var selectCmd = RelationalCommandDefinition.Idempotent(
                "SELECT Description FROM NullTest WHERE Id = @Id;",
                new { Id = "n1" });

            string? desc = await _provider.ExecuteScalarAsync<string>(selectCmd);
            Assert.Null(desc);
        }

        [Fact]
        public async Task CheckHealthAsync_ReturnsHealthyWhenDatabaseIsAccessible()
        {
            DatabaseHealthResult health = await _provider.CheckHealthAsync();

            Assert.True(health.IsHealthy);
            Assert.True(health.Latency >= TimeSpan.Zero);
            Assert.Null(health.Exception);
        }

        [Fact]
        public async Task DisposedProvider_ThrowsObjectDisposedException()
        {
            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = "Data Source=:memory:"
            };

            var provider = new SqliteTestDatabaseProvider(settings);
            provider.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.OpenConnectionAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.CheckHealthAsync());
        }
    }
}
