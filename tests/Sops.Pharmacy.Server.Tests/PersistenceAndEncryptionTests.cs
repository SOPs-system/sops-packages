using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Persistence;
using Xunit;

namespace Sops.Pharmacy.Server.Tests
{
    public class PersistenceAndEncryptionTests : IDisposable
    {
        private readonly string _testDir;
        private readonly PharmacyServerSettings _settings;

        public PersistenceAndEncryptionTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "sops_test_db_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            _settings = new PharmacyServerSettings
            {
                DatabasePath = Path.Combine(_testDir, "test_pharmacy.db"),
                BackupDirectory = Path.Combine(_testDir, "backups"),
                LogDirectory = Path.Combine(_testDir, "logs"),
                DatabasePassphrase = "Test_Secret_Passphrase_2026!"
            };
            _settings.EnsureDirectories();
        }

        [Fact]
        public async Task SqlCipher_DatabaseInitialization_EnforcesEncryptionAndMigrations()
        {
            using var provider = new SqlCipherDatabaseProvider(_settings);
            var migrator = new DatabaseMigrator(provider, _settings);

            // Run migrations
            await migrator.MigrateAsync();

            // Verify integrity check
            var health = await provider.CheckHealthAsync();
            Assert.True(health.IsHealthy, health.Description);

            // Verify tables exist
            using (var conn = await provider.OpenSqliteConnectionAsync())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('local_drugs', 'stock_lots', 'pos_transactions', 'pos_shifts', 'registered_terminals', 'local_users', 'audit_events', 'outbox_events');";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(8, count);

                // Verify default admin user was seeded
                cmd.CommandText = "SELECT username, role, is_active FROM local_users WHERE username = 'admin';";
                using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("admin", reader.GetString(0));
                Assert.Equal("Admin", reader.GetString(1));
                Assert.Equal(1, reader.GetInt32(2));
            }
        }

        [Fact]
        public async Task SqlCipher_OpeningWithoutKey_FailsCryptographically()
        {
            using (var provider = new SqlCipherDatabaseProvider(_settings))
            {
                var migrator = new DatabaseMigrator(provider, _settings);
                await migrator.MigrateAsync();
            }

            // Attempt opening the encrypted file with plain SQLite without PRAGMA key
            SQLitePCL.Batteries_V2.Init();
            using var conn = new SqliteConnection($"Data Source={_settings.DatabasePath};");
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";

            // Standard SQLite without key will throw SqliteException (file is not a database)
            await Assert.ThrowsAnyAsync<SqliteException>(async () =>
            {
                await cmd.ExecuteScalarAsync();
            });
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }
    }
}
