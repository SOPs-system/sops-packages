using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Cache.Abstractions;
using Sops.Cache.Memory;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Authentication;
using Sops.DataLayer.Authorization;
using Sops.DataLayer.Client;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Connectivity;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Events;
using Sops.DataLayer.Files;
using Sops.DataLayer.Offline;
using Sops.DataLayer.Pipeline;
using Sops.DataLayer.Services;
using Sops.DataLayer.Transport;
using Sops.DataLayer.Wpf;

namespace Sops.DataLayer.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring and registering Sops.DataLayer in Microsoft.Extensions.DependencyInjection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSopsDataLayer(
            this IServiceCollection services,
            Action<DataLayerOptions>? configureOptions = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<DataLayerOptions>(_ => { });
            }

            services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<DataLayerOptions>>().Value);

            services.TryAddSingleton<HttpClient>(sp => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

            services.TryAddSingleton<IUiDispatcher, WpfDispatcherAdapter>();

            services.TryAddSingleton<IEndpointRegistry, EndpointRegistry>();
            services.TryAddSingleton<IEndpointResolver, EndpointResolver>();

            services.TryAddSingleton<ITransportFactory, TransportFactory>();

            services.TryAddSingleton<ISecureCredentialStore, SecureCredentialStore>();
            services.TryAddSingleton<ISessionManager, SessionManager>();
            services.TryAddSingleton<IAuthorizationStateProvider, PermissionClient>();

            services.TryAddSingleton<ICacheService>(sp => new MemoryCacheService());

            services.TryAddSingleton<IOfflineReadCoordinator, OfflineReadCoordinator>();

            services.TryAddTransient<CorrelationMiddleware>();
            services.TryAddTransient<ValidationMiddleware>();
            services.TryAddTransient<AuthenticationMiddleware>();
            services.TryAddTransient<ResilienceMiddleware>();

            services.TryAddSingleton<IRequestPipeline>(sp =>
            {
                var registry = sp.GetRequiredService<IEndpointRegistry>();
                var resolver = sp.GetRequiredService<IEndpointResolver>();
                var transportFactory = sp.GetRequiredService<ITransportFactory>();
                var offlineCoordinator = sp.GetService<IOfflineReadCoordinator>();
                var logger = sp.GetService<ILogger<RequestPipeline>>();
                var options = sp.GetRequiredService<DataLayerOptions>();

                var pipeline = new RequestPipeline(registry, resolver, transportFactory, offlineCoordinator, null, logger);

                pipeline.AddMiddleware(new CorrelationMiddleware(options));
                pipeline.AddMiddleware(new ValidationMiddleware());

                var sessionMgr = sp.GetRequiredService<ISessionManager>();
                var authz = sp.GetRequiredService<IAuthorizationStateProvider>();
                pipeline.AddMiddleware(new AuthenticationMiddleware(sessionMgr, authz, null, sp.GetService<ILogger<AuthenticationMiddleware>>()));

                pipeline.AddMiddleware(new ResilienceMiddleware(options, null, null, sp.GetService<ILogger<ResilienceMiddleware>>()));

                return pipeline;
            });

            services.TryAddSingleton<IDataLayerClient, DataLayerClient>();
            services.TryAddSingleton<IAuthenticationService, AuthenticationCoordinator>();

            services.TryAddSingleton<IDataLayerEventBus, DataLayerEventBus>();
            services.TryAddSingleton<ILivePollingClient, LivePollingClient>();

            services.TryAddSingleton<IConnectivityMonitor, ConnectivityMonitor>();

            services.TryAddSingleton<IFileTransferClient, FileTransferClient>();

            services.TryAddSingleton<IDrugDataService, DrugDataService>();
            services.TryAddSingleton<IPatientDataService, PatientDataService>();
            services.TryAddSingleton<IInventoryDataService, InventoryDataService>();
            services.TryAddSingleton<ITransactionDataService, TransactionDataService>();
            services.TryAddSingleton<IShiftDataService, ShiftDataService>();
            services.TryAddSingleton<IAuthDataService, AuthDataService>();
            services.TryAddSingleton<ISettingsDataService, SettingsDataService>();
            services.TryAddSingleton<ICategoryDataService, CategoryDataService>();
            services.TryAddSingleton<IWarehouseDataService, WarehouseDataService>();
            services.TryAddSingleton<ILicensingDataService, LicensingDataService>();
            services.TryAddSingleton<ISyncDataService, SyncDataService>();

            services.TryAddSingleton<ISopsDataLayer, SopsDataLayer>();

            return services;
        }
    }
}
