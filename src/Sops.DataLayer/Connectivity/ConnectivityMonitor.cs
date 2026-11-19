using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Connectivity;

namespace Sops.DataLayer.Connectivity
{
    /// <summary>
    /// Independent connectivity monitor tracking local server and global cloud API reachability.
    /// </summary>
    public sealed class ConnectivityMonitor : IConnectivityMonitor
    {
        private readonly HttpClient _httpClient;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IEndpointRegistry _endpointRegistry;
        private readonly DataLayerOptions _options;
        private readonly ILogger<ConnectivityMonitor>? _logger;

        private readonly object _stateLock = new object();
        private ConnectionState _state = ConnectionState.Unknown;
        private ServerHealth _localServerHealth = ServerHealth.Unreachable(BackendTarget.LocalServer, "Not checked");
        private ServerHealth _cloudApiHealth = ServerHealth.Unreachable(BackendTarget.GlobalApi, "Not checked");

        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private bool _disposed;

        public ConnectionState State
        {
            get
            {
                lock (_stateLock) return _state;
            }
        }

        public ServerHealth LocalServerHealth
        {
            get
            {
                lock (_stateLock) return _localServerHealth;
            }
        }

        public ServerHealth CloudApiHealth
        {
            get
            {
                lock (_stateLock) return _cloudApiHealth;
            }
        }

        public event EventHandler<ConnectionState>? StateChanged;

        public ConnectivityMonitor(
            HttpClient httpClient,
            IEndpointResolver endpointResolver,
            IEndpointRegistry endpointRegistry,
            DataLayerOptions options,
            ILogger<ConnectivityMonitor>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _endpointRegistry = endpointRegistry ?? throw new ArgumentNullException(nameof(endpointRegistry));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public void StartMonitoring()
        {
            lock (_stateLock)
            {
                if (_pollTask != null && !_pollTask.IsCompleted) return;

                _cts = new CancellationTokenSource();
                _pollTask = Task.Run(() => PollingLoopAsync(_cts.Token));
            }
        }

        public void StopMonitoring()
        {
            CancellationTokenSource? ctsToCancel = null;
            Task? taskToAwait = null;

            lock (_stateLock)
            {
                ctsToCancel = _cts;
                taskToAwait = _pollTask;
                _cts = null;
                _pollTask = null;
            }

            if (ctsToCancel != null)
            {
                ctsToCancel.Cancel();
                ctsToCancel.Dispose();
            }

            if (taskToAwait != null)
            {
                try
                {
                    taskToAwait.GetAwaiter().GetResult();
                }
                catch { }
            }
        }

        public async Task CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var localHealth = await ProbeTargetAsync(BackendTarget.LocalServer, cancellationToken).ConfigureAwait(false);
            ServerHealth cloudHealth = _cloudApiHealth;

            if (_options.GlobalApiAddress != null)
            {
                cloudHealth = await ProbeTargetAsync(BackendTarget.GlobalApi, cancellationToken).ConfigureAwait(false);
            }

            ConnectionState combinedState;
            EventHandler<ConnectionState>? handler = null;

            lock (_stateLock)
            {
                _localServerHealth = localHealth;
                _cloudApiHealth = cloudHealth;

                if (localHealth.IsReachable && cloudHealth.IsReachable)
                {
                    combinedState = ConnectionState.LocalAndCloudAvailable;
                }
                else if (localHealth.IsReachable)
                {
                    combinedState = ConnectionState.LocalOnly;
                }
                else if (cloudHealth.IsReachable)
                {
                    combinedState = ConnectionState.CloudOnly;
                }
                else
                {
                    combinedState = ConnectionState.FullyOffline;
                }

                if (_state != combinedState)
                {
                    _state = combinedState;
                    handler = StateChanged;
                }
            }

            handler?.Invoke(this, combinedState);
        }

        private async Task<ServerHealth> ProbeTargetAsync(BackendTarget target, CancellationToken cancellationToken)
        {
            string endpointId = target == BackendTarget.LocalServer ? "system.health" : "global.health";

            if (!_endpointRegistry.TryGet(endpointId, out var endpoint))
            {
                if (target == BackendTarget.LocalServer)
                {
                    endpoint = new EndpointDefinition("system.health", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/health", requiresAuthentication: false);
                }
                else
                {
                    return ServerHealth.Unreachable(target, "No global health endpoint configured");
                }
            }

            var uri = _endpointResolver.ResolveUri(endpoint!);
            var sw = Stopwatch.StartNew();

            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(3));
                    using (var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false))
                    {
                        sw.Stop();
                        return resp.IsSuccessStatusCode
                            ? ServerHealth.Healthy(target, sw.Elapsed, $"HTTP {(int)resp.StatusCode}")
                            : ServerHealth.Unreachable(target, $"HTTP {(int)resp.StatusCode}", sw.Elapsed);
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return ServerHealth.Unreachable(target, ex.Message, sw.Elapsed);
            }
        }

        private async Task PollingLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckHealthAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Error during health poll");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopMonitoring();
            }
        }
    }
}
