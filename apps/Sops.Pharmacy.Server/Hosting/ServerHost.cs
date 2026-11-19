using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Domain.Auth;
using Sops.Pharmacy.Server.Domain.Catalog;
using Sops.Pharmacy.Server.Domain.Devices;
using Sops.Pharmacy.Server.Domain.Inventory;
using Sops.Pharmacy.Server.Domain.Sales;
using Sops.Pharmacy.Server.Domain.Shifts;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Hosting
{
    public class ServerHost : IDisposable
    {
        private readonly PharmacyServerSettings _settings;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServerHost> _logger;
        private RpcServer? _rpcServer;
        private Timer? _backupTimer;
        private bool _isDisposed;

        public bool IsRunning => _rpcServer != null && _rpcServer.IsRunning;
        public IServiceProvider Services => _serviceProvider;

        public ServerHost(PharmacyServerSettings? settings = null)
        {
            _settings = settings ?? ServerConfigLoader.Load();
            _settings.EnsureDirectories();

            var services = new ServiceCollection();
            ConfigureServices(services, _settings);
            _serviceProvider = services.BuildServiceProvider();

            _logger = _serviceProvider.GetRequiredService<ILogger<ServerHost>>();
        }

        private static void ConfigureServices(IServiceCollection services, PharmacyServerSettings settings)
        {
            services.AddSingleton(settings);
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Persistence
            services.AddSingleton<SqlCipherDatabaseProvider>();
            services.AddSingleton<DatabaseMigrator>();
            services.AddSingleton<SqlCipherOutboxStore>();
            services.AddSingleton<SqlCipherInboxStore>();

            // Domain Services
            services.AddSingleton<IDrugCatalogService, DrugCatalogService>();
            services.AddSingleton<IInventoryService, InventoryService>();
            services.AddSingleton<ISalesService, SalesService>();
            services.AddSingleton<IShiftService, ShiftService>();
            services.AddSingleton<IDeviceService, DeviceService>();
            services.AddSingleton<IAuthService, AuthService>();

            // Hosting
            services.AddSingleton<RpcServer>();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting SOPs Pharmacy Local Server Host on port {Port}...", _settings.Port);

            // 1. Run database migrations and seeding
            var migrator = _serviceProvider.GetRequiredService<DatabaseMigrator>();
            await migrator.MigrateAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Authoritative SQLCipher schema verified and up to date.");

            // 2. Start RPC Server
            _rpcServer = _serviceProvider.GetRequiredService<RpcServer>();
            _rpcServer.Start();

            // 3. Setup periodic background backup timer
            var interval = TimeSpan.FromHours(Math.Max(1, _settings.BackupIntervalHours));
            _backupTimer = new Timer(async _ => await RunScheduledBackupAsync(), null, interval, interval);

            _logger.LogInformation("SOPs Pharmacy Local Server is fully operational.");
        }

        private async Task RunScheduledBackupAsync()
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupFile = Path.Combine(_settings.BackupDirectory, $"sops_pharmacy_backup_{timestamp}.bak");

                using (var sourceStream = new FileStream(_settings.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var destStream = new FileStream(backupFile, FileMode.Create, FileAccess.Write))
                {
                    await sourceStream.CopyToAsync(destStream).ConfigureAwait(false);
                }

                _logger.LogInformation("Scheduled encrypted backup created at '{Path}'", backupFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed.");
            }
        }

        public void Stop()
        {
            _logger.LogInformation("Stopping SOPs Pharmacy Local Server Host...");
            _backupTimer?.Dispose();
            _backupTimer = null;
            _rpcServer?.Stop();
            _rpcServer = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            if (_serviceProvider is IDisposable d)
            {
                d.Dispose();
            }
        }
    }
}
