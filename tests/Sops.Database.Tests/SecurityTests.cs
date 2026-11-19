using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sops.Configuration.Models;
using Sops.Database.Configuration;
using Sops.Database.Relational;
using Sops.Database.Tests.TestHelpers;
using Xunit;

namespace Sops.Database.Tests
{
    public class SecurityTests : IDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly SqliteTestDatabaseProvider _provider;
        private readonly string _secretPassword = "SuperSecretPassword123!";

        public SecurityTests()
        {
            string dbName = "sec_test_" + Guid.NewGuid().ToString("N");
            string connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();

            _loggerProvider = new TestLoggerProvider();
            var logger = _loggerProvider.CreateLogger("Test");

            var settings = new DatabaseSettings
            {
                Provider = "Sqlite",
                ConnectionString = connectionString
            };

            _provider = new SqliteTestDatabaseProvider(settings, logger);

            _provider.ExecuteNonQueryAsync(RelationalCommandDefinition.Create(
                "CREATE TABLE Accounts (Id TEXT PRIMARY KEY, Username TEXT);")).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _provider.Dispose();
            _keepAliveConnection.Dispose();
        }

        [Fact]
        public void ConnectionString_HasSensitiveAttribute()
        {
            PropertyInfo? prop = typeof(DatabaseSettings).GetProperty(nameof(DatabaseSettings.ConnectionString));
            Assert.NotNull(prop);

            var attr = prop.GetCustomAttribute<SensitiveAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void SafeSummary_DoesNotLeakPassword()
        {
            var settings = new DatabaseSettings
            {
                Provider = "PostgreSql",
                ConnectionString = $"Host=db.prod;Username=app;Password={_secretPassword}"
            };

            string summary = settings.ToSafeSummary();

            Assert.DoesNotContain(_secretPassword, summary);
            Assert.Contains("[REDACTED]", summary);
        }

        [Fact]
        public void Logger_DoesNotOutput_ConnectionStringOrPassword()
        {
            foreach (string log in _loggerProvider.Logs)
            {
                Assert.DoesNotContain(_secretPassword, log);
            }
        }

        [Fact]
        public async Task ParameterizedCommand_PreventsSqlInjection()
        {
            // Untrusted user input attempting SQL injection
            string maliciousInput = "admin'; DROP TABLE Accounts; --";

            var insertCmd = RelationalCommandDefinition.Create(
                "INSERT INTO Accounts (Id, Username) VALUES (@Id, @Username);",
                new { Id = "acc1", Username = maliciousInput });

            int rows = await _provider.ExecuteNonQueryAsync(insertCmd);
            Assert.Equal(1, rows);

            // Table Accounts should still exist and contain the literal string
            var queryCmd = RelationalCommandDefinition.Idempotent(
                "SELECT Username FROM Accounts WHERE Id = @Id;",
                new { Id = "acc1" });

            string? username = await _provider.ExecuteScalarAsync<string>(queryCmd);
            Assert.Equal(maliciousInput, username);

            // Verify Accounts table is intact
            long? count = await _provider.ExecuteScalarAsync<long>(
                RelationalCommandDefinition.Idempotent("SELECT COUNT(*) FROM Accounts;"));
            Assert.Equal(1L, count);
        }

        private class TestLoggerProvider : ILoggerProvider
        {
            public List<string> Logs { get; } = new List<string>();

            public ILogger CreateLogger(string categoryName) => new TestLogger(Logs);

            public void Dispose() { }
        }

        private class TestLogger : ILogger
        {
            private readonly List<string> _logs;

            public TestLogger(List<string> logs) => _logs = logs;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _logs.Add(formatter(state, exception));
            }
        }
    }
}
