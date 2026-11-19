using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Domain.Auth;
using Sops.Pharmacy.Server.Domain.Catalog;
using Sops.Pharmacy.Server.Domain.Devices;
using Sops.Pharmacy.Server.Domain.Inventory;
using Sops.Pharmacy.Server.Domain.Sales;
using Sops.Pharmacy.Server.Domain.Shifts;
using Sops.Pharmacy.Server.Hosting;
using Sops.Pharmacy.Server.Persistence;
using Xunit;

namespace Sops.Pharmacy.Server.Tests
{
    public class RpcServerTests : IDisposable
    {
        private readonly string _testDir;
        private readonly PharmacyServerSettings _settings;
        private readonly ServerHost _host;
        private readonly HttpClient _client;

        public RpcServerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "sops_test_rpc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            // Use test port 5048
            _settings = new PharmacyServerSettings
            {
                Port = 5048,
                DatabasePath = Path.Combine(_testDir, "rpc_test.db"),
                BackupDirectory = Path.Combine(_testDir, "backups"),
                LogDirectory = Path.Combine(_testDir, "logs"),
                DatabasePassphrase = "Rpc_Secret_Passphrase_2026!"
            };

            _host = new ServerHost(_settings);
            _host.StartAsync().GetAwaiter().GetResult();

            _client = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{_settings.Port}/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        [Fact]
        public async Task RpcServer_HealthEndpoint_Returns200AndHealthyStatus()
        {
            var response = await _client.GetAsync("health");
            Assert.True(response.IsSuccessStatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();
            Assert.Equal("Healthy", status);
        }

        [Fact]
        public async Task RpcServer_InfoEndpoint_ReturnsServerMetadata()
        {
            var response = await _client.GetAsync("rpc/info");
            Assert.True(response.IsSuccessStatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var serverName = data.GetProperty("serverName").GetString();
            Assert.Equal("SOPs Pharmacy Local Server", serverName);
        }

        [Fact]
        public async Task RpcServer_ManagementStatusEndpoint_ReturnsTelemetry()
        {
            var response = await _client.GetAsync("rpc/management/status");
            Assert.True(response.IsSuccessStatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            Assert.True(data.GetProperty("port").GetInt32() == 5048);
            Assert.True(data.GetProperty("activeTerminalsCount").GetInt32() >= 0);
        }

        public void Dispose()
        {
            _client?.Dispose();
            _host?.Stop();
            _host?.Dispose();
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
