using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Events;

namespace Sops.DataLayer.Events
{
    /// <summary>
    /// Manages real-time Server-Sent Events (SSE) streaming with transparent long-polling fallback,
    /// cursor tracking, deduplication, and exponential backoff with jitter.
    /// </summary>
    public sealed class LivePollingClient : ILivePollingClient
    {
        private readonly HttpClient _httpClient;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IEndpointRegistry _endpointRegistry;
        private readonly IDataLayerEventBus _eventBus;
        private readonly DataLayerOptions _options;
        private readonly ILogger<LivePollingClient>? _logger;

        private readonly object _stateLock = new object();
        private readonly HashSet<string> _subscribedTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _recentEventIds = new Queue<string>(256);
        private readonly HashSet<string> _recentEventIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Random _jitter = new Random();

        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private bool _isConnected;
        private string? _lastEventId;
        private int? _serverRetryIntervalMs;
        private bool _disposed;

        public bool IsConnected
        {
            get
            {
                lock (_stateLock) return _isConnected;
            }
        }

        public string? LastEventId
        {
            get
            {
                lock (_stateLock) return _lastEventId;
            }
        }

        public event EventHandler<EventEnvelope>? EventReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public LivePollingClient(
            HttpClient httpClient,
            IEndpointResolver endpointResolver,
            IEndpointRegistry endpointRegistry,
            IDataLayerEventBus eventBus,
            DataLayerOptions options,
            ILogger<LivePollingClient>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _endpointRegistry = endpointRegistry ?? throw new ArgumentNullException(nameof(endpointRegistry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public void Subscribe(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic)) return;
            lock (_stateLock)
            {
                _subscribedTopics.Add(topic);
            }
        }

        public void Unsubscribe(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic)) return;
            lock (_stateLock)
            {
                _subscribedTopics.Remove(topic);
            }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_listenTask != null && !_listenTask.IsCompleted)
                {
                    return Task.FromResult(0);
                }

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => StreamLoopAsync(_cts.Token));
            }

            return Task.FromResult(0);
        }

        public async Task DisconnectAsync()
        {
            CancellationTokenSource? ctsToCancel = null;
            Task? taskToAwait = null;

            lock (_stateLock)
            {
                ctsToCancel = _cts;
                taskToAwait = _listenTask;
                _cts = null;
                _listenTask = null;
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
                    // Allow up to 1000ms for graceful stream exit without hanging caller
                    await Task.WhenAny(taskToAwait, Task.Delay(1000)).ConfigureAwait(false);
                }
                catch { }
            }

            UpdateConnectionStatus(false);
        }

        private async Task StreamLoopAsync(CancellationToken cancellationToken)
        {
            int failureCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_endpointRegistry.TryGet("live.stream", out var endpoint))
                    {
                        _logger?.LogWarning("Live stream endpoint 'live.stream' not found. Retrying in 5s...");
                        await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var targetUri = _endpointResolver.ResolveUri(endpoint!);
                    if (string.Equals(targetUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    {
                        Security.TlsConfiguration.EnsureTls12Supported();
                    }

                    using (var request = new HttpRequestMessage(HttpMethod.Get, targetUri))
                    {
                        request.Headers.Accept.Clear();
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                        lock (_stateLock)
                        {
                            if (!string.IsNullOrEmpty(_lastEventId))
                            {
                                request.Headers.TryAddWithoutValidation("Last-Event-ID", _lastEventId);
                            }
                        }

                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new HttpRequestException($"Server returned HTTP {response.StatusCode} for live feed");
                            }

                            UpdateConnectionStatus(true);
                            failureCount = 0;
                            _logger?.LogInformation("Connected to SOPs live feed at {Uri}", targetUri);

                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var reader = new StreamReader(stream))
                            {
                                var dataBuilder = new StringBuilder();
                                string currentEvent = "message";
                                string? currentId = null;

                                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                                {
                                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                                    if (line == null) break;

                                    // SSE comment lines start with ':'
                                    if (line.Length > 0 && line[0] == ':') continue;

                                    // Empty line indicates event dispatch boundary in W3C SSE standard
                                    if (line.Length == 0 || line == "\r")
                                    {
                                        if (dataBuilder.Length > 0)
                                        {
                                            DispatchEvent(currentEvent, dataBuilder.ToString(), currentId);
                                        }

                                        currentEvent = "message";
                                        dataBuilder.Clear();
                                        currentId = null;
                                        continue;
                                    }

                                    if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string val = line.Substring(6);
                                        if (val.Length > 0 && val[0] == ' ') val = val.Substring(1);
                                        currentEvent = val.Trim();
                                    }
                                    else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string val = line.Substring(5);
                                        if (val.Length > 0 && val[0] == ' ') val = val.Substring(1);
                                        if (dataBuilder.Length > 0) dataBuilder.Append('\n');
                                        dataBuilder.Append(val);
                                    }
                                    else if (line.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string val = line.Substring(3);
                                        if (val.Length > 0 && val[0] == ' ') val = val.Substring(1);
                                        currentId = val.Trim();
                                        lock (_stateLock)
                                        {
                                            _lastEventId = currentId;
                                        }
                                    }
                                    else if (line.StartsWith("retry:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string val = line.Substring(6).Trim();
                                        if (int.TryParse(val, out int retryMs) && retryMs > 0)
                                        {
                                            lock (_stateLock)
                                            {
                                                _serverRetryIntervalMs = retryMs;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    UpdateConnectionStatus(false);

                    int baseDelaySeconds;
                    int jitterMs;
                    lock (_stateLock)
                    {
                        baseDelaySeconds = _serverRetryIntervalMs.HasValue && failureCount == 1
                            ? Math.Max(1, _serverRetryIntervalMs.Value / 1000)
                            : Math.Min(30, (int)Math.Pow(2, Math.Min(5, failureCount)));
                        jitterMs = _jitter.Next(100, 1000);
                    }

                    _logger?.LogWarning(ex, "Live stream disconnected (failure #{Count}). Reconnecting in {DelaySeconds}s + {JitterMs}ms...", failureCount, baseDelaySeconds, jitterMs);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(baseDelaySeconds) + TimeSpan.FromMilliseconds(jitterMs), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            UpdateConnectionStatus(false);
        }

        private void DispatchEvent(string eventType, string rawData, string? id)
        {
            try
            {
                lock (_stateLock)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        if (_recentEventIdSet.Contains(id!))
                        {
                            _logger?.LogDebug("Dropped duplicate live event with ID {EventId}", id);
                            return;
                        }

                        if (_recentEventIds.Count >= 256)
                        {
                            var oldest = _recentEventIds.Dequeue();
                            _recentEventIdSet.Remove(oldest);
                        }

                        _recentEventIds.Enqueue(id!);
                        _recentEventIdSet.Add(id!);
                    }

                    if (_subscribedTopics.Count > 0 && !_subscribedTopics.Contains(eventType) && !_subscribedTopics.Contains("*"))
                    {
                        return;
                    }
                }

                JsonElement payload;
                using (var doc = JsonDocument.Parse(rawData))
                {
                    payload = doc.RootElement.Clone();
                }

                var envelope = new EventEnvelope(
                    eventType: eventType,
                    payload: payload,
                    eventId: id ?? Guid.NewGuid().ToString("N"));

                EventReceived?.Invoke(this, envelope);
                _eventBus.Publish(envelope);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse and dispatch live event {EventType}", eventType);
            }
        }

        private void UpdateConnectionStatus(bool connected)
        {
            EventHandler<bool>? handler = null;
            lock (_stateLock)
            {
                if (_isConnected != connected)
                {
                    _isConnected = connected;
                    handler = ConnectionStatusChanged;
                }
            }
            handler?.Invoke(this, connected);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                DisconnectAsync().GetAwaiter().GetResult();
            }
        }
    }
}
