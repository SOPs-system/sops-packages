using System;
using System.IO;
using System.Text.Json;

namespace Sops.Pharmacy.Server.Configuration
{
    public static class ServerConfigLoader
    {
        public static PharmacyServerSettings Load(string? customPath = null)
        {
            var configPath = customPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SOPs", "Server", "config.json");

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var settings = JsonSerializer.Deserialize<PharmacyServerSettings>(json);
                    if (settings != null)
                    {
                        settings.EnsureDirectories();
                        return settings;
                    }
                }
                catch
                {
                    // Fall back to defaults on corrupt config file
                }
            }

            var defaultSettings = new PharmacyServerSettings();
            defaultSettings.EnsureDirectories();

            try
            {
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configPath, JsonSerializer.Serialize(defaultSettings, options));
            }
            catch
            {
                // Proceed with in-memory defaults if filesystem write is blocked
            }

            return defaultSettings;
        }
    }
}
