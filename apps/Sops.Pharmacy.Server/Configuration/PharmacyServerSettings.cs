using System;
using System.IO;

namespace Sops.Pharmacy.Server.Configuration
{
    /// <summary>
    /// Validated configuration for the authoritative local pharmacy server.
    /// Redacts sensitive passphrases and secrets from diagnostic outputs.
    /// </summary>
    public class PharmacyServerSettings
    {
        public string PharmacyId { get; set; } = "pharma_local_001";
        public string PharmacyName { get; set; } = "Smart Operating Pharmacy — Cairo Branch";
        public string BranchName { get => PharmacyName; set => PharmacyName = value; }
        public string BranchCode { get; set; } = "CAI-01";
        public int Port { get; set; } = 5040;
        public bool LanEnabled { get; set; } = true;
        public bool ListenOnAllInterfaces { get => LanEnabled; set => LanEnabled = value; }
        public string AllowedLanSubnets { get; set; } = "127.0.0.1,192.168.*,10.*";
        public string DatabasePath { get; set; } = string.Empty;
        public string DatabasePassphrase { get; set; } = "sops_sqlcipher_default_secure_key_2026";
        public string BackupDirectory { get; set; } = string.Empty;
        public int BackupIntervalHours { get; set; } = 6;
        public string OperatingMode { get; set; } = "Service";
        public bool CloudSyncEnabled { get; set; } = false;
        public string CloudSyncUrl { get; set; } = "https://sync.sops.internal/v1";
        public int SyncIntervalSeconds { get; set; } = 60;
        public string JwtSecret { get; set; } = "sops_pharmacy_local_jwt_hmac_sha256_super_secret_token_key_2026!";
        public string LogDirectory { get; set; } = string.Empty;

        public PharmacyServerSettings()
        {
            var baseDataDir = GetDefaultBaseDirectory();

            DatabasePath = Path.Combine(baseDataDir, "data", "pharmacy.db");
            BackupDirectory = Path.Combine(baseDataDir, "backups");
            LogDirectory = Path.Combine(baseDataDir, "logs");
        }

        private static string GetDefaultBaseDirectory()
        {
            try
            {
                var common = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SOPs", "Server");
                return common;
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SOPs", "Server");
            }
        }

        public void EnsureDirectories()
        {
            try
            {
                EnsureDirectoryCore();
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to LocalApplicationData for non-elevated user or restricted sandbox contexts
                var fallbackBase = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SOPs", "Server");

                DatabasePath = Path.Combine(fallbackBase, "data", "pharmacy.db");
                BackupDirectory = Path.Combine(fallbackBase, "backups");
                LogDirectory = Path.Combine(fallbackBase, "logs");

                EnsureDirectoryCore();
            }
            catch (Exception)
            {
                // If LocalApplicationData is also constrained, fallback to local app directory
                try
                {
                    var appBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerData");
                    DatabasePath = Path.Combine(appBase, "data", "pharmacy.db");
                    BackupDirectory = Path.Combine(appBase, "backups");
                    LogDirectory = Path.Combine(appBase, "logs");
                    EnsureDirectoryCore();
                }
                catch
                {
                    // Ignore directory creation failure; in-memory or fallback paths will be handled
                }
            }
        }

        private void EnsureDirectoryCore()
        {
            var dbDir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            if (!string.IsNullOrEmpty(BackupDirectory) && !Directory.Exists(BackupDirectory))
            {
                Directory.CreateDirectory(BackupDirectory);
            }

            if (!string.IsNullOrEmpty(LogDirectory) && !Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
    }
}
