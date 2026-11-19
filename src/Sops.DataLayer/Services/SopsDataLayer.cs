using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;

namespace Sops.DataLayer.Services
{
    /// <summary>
    /// Unified main DataLayer facade for WPF and desktop client applications.
    /// </summary>
    public sealed class SopsDataLayer : ISopsDataLayer, IDisposable
    {
        public IDrugDataService Drugs { get; }
        public IPatientDataService Patients { get; }
        public IInventoryDataService Inventory { get; }
        public ITransactionDataService Transactions { get; }
        public IShiftDataService Shifts { get; }
        public IAuthDataService Auth { get; }
        public ISettingsDataService Settings { get; }
        public ICategoryDataService Categories { get; }
        public IWarehouseDataService Warehouses { get; }
        public ILicensingDataService Licensing { get; }
        public ISyncDataService Sync { get; }

        public IConnectivityMonitor Connectivity { get; }
        public IDataLayerEventBus Events { get; }
        public ILivePollingClient Live { get; }
        public IOfflineReadCoordinator Cache { get; }
        public IFileTransferClient Files { get; }
        public IAuthenticationService Authentication { get; }
        public IAuthorizationStateProvider Authorization { get; }
        public IDataLayerClient Client { get; }

        private readonly ILogger<SopsDataLayer>? _logger;
        private bool _disposed;

        public SopsDataLayer(
            IDrugDataService drugs,
            IPatientDataService patients,
            IInventoryDataService inventory,
            ITransactionDataService transactions,
            IShiftDataService shifts,
            IAuthDataService auth,
            ISettingsDataService settings,
            ICategoryDataService categories,
            IWarehouseDataService warehouses,
            ILicensingDataService licensing,
            ISyncDataService sync,
            IConnectivityMonitor connectivity,
            IDataLayerEventBus events,
            ILivePollingClient live,
            IOfflineReadCoordinator cache,
            IFileTransferClient files,
            IAuthenticationService authentication,
            IAuthorizationStateProvider authorization,
            IDataLayerClient client,
            ILogger<SopsDataLayer>? logger = null)
        {
            Drugs = drugs ?? throw new ArgumentNullException(nameof(drugs));
            Patients = patients ?? throw new ArgumentNullException(nameof(patients));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            Shifts = shifts ?? throw new ArgumentNullException(nameof(shifts));
            Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Categories = categories ?? throw new ArgumentNullException(nameof(categories));
            Warehouses = warehouses ?? throw new ArgumentNullException(nameof(warehouses));
            Licensing = licensing ?? throw new ArgumentNullException(nameof(licensing));
            Sync = sync ?? throw new ArgumentNullException(nameof(sync));

            Connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Live = live ?? throw new ArgumentNullException(nameof(live));
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Files = files ?? throw new ArgumentNullException(nameof(files));
            Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting SopsDataLayer background services...");
            Connectivity.StartMonitoring();

            Live.EventReceived += async (s, e) =>
            {
                if (e.EventType.StartsWith("inventory.", StringComparison.OrdinalIgnoreCase) ||
                    e.EventType.StartsWith("drug.", StringComparison.OrdinalIgnoreCase) ||
                    e.EventType.StartsWith("catalog.", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogInformation("Live event {EventType} received; invalidating client cache", e.EventType);
                    try
                    {
                        await Cache.InvalidateCacheAsync(e.EventType).ConfigureAwait(false);
                    }
                    catch { }
                }
            };

            await Live.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("SopsDataLayer started successfully.");
        }

        public async Task StopAsync()
        {
            _logger?.LogInformation("Stopping SopsDataLayer background services...");
            Connectivity.StopMonitoring();
            await Live.DisconnectAsync().ConfigureAwait(false);
            _logger?.LogInformation("SopsDataLayer stopped.");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopAsync().GetAwaiter().GetResult();
                Connectivity.Dispose();
                Live.Dispose();
            }
        }
    }
}
