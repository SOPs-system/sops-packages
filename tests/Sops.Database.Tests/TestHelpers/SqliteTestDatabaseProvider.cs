using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;
using Sops.Database.Relational;

namespace Sops.Database.Tests.TestHelpers
{
    public class SqliteTestDatabaseProvider : RelationalDatabaseProviderBase
    {
        public override string ProviderName => "Sqlite";

        public SqliteTestDatabaseProvider(
            DatabaseSettings settings,
            ILogger? logger = null,
            DatabaseCapabilities? capabilities = null)
            : base(settings, logger, capabilities)
        {
        }

        public override DbConnection CreateConnection()
        {
            return new SqliteConnection(Settings.ConnectionString);
        }

        protected override string GetHealthProbeCommandText() => "SELECT 1;";
    }
}
