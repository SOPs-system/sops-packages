using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Connectivity;
using Sops.DataLayer.Models.Events;
using Sops.DataLayer.Models.Files;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Abstractions
{
    /// <summary>
    /// Independent connectivity monitor tracking local server and global cloud API reachability.
    /// </summary>
    public interface IConnectivityMonitor : IDisposable
    {
        ConnectionState State { get; }
        ServerHealth LocalServerHealth { get; }
        ServerHealth CloudApiHealth { get; }

        event EventHandler<ConnectionState>? StateChanged;

        Task CheckHealthAsync(CancellationToken cancellationToken = default);
        void StartMonitoring();
        void StopMonitoring();
    }

    /// <summary>
    /// In-process thread-safe event bus with exception isolation and UI dispatcher compatibility.
    /// </summary>
    public interface IDataLayerEventBus
    {
        void Publish<TEvent>(TEvent eventData);
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);
        IDisposable SubscribeOnUi<TEvent>(Action<TEvent> handler);
        void ClearSubscriptions();
    }

    /// <summary>
    /// Manages real-time Server-Sent Events (SSE) and long-polling connection to the local server (/api/live).
    /// </summary>
    public interface ILivePollingClient : IDisposable
    {
        bool IsConnected { get; }
        event EventHandler<EventEnvelope>? EventReceived;
        event EventHandler<bool>? ConnectionStatusChanged;

        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync();
        void Subscribe(string topic);
        void Unsubscribe(string topic);
    }

    /// <summary>
    /// Coordinates offline read strategies, caching, and stale result presentation.
    /// </summary>
    public interface IOfflineReadCoordinator
    {
        Task<DataResult<T>> ExecuteWithCachePolicyAsync<T>(
            EndpointDefinition endpoint,
            DataRequest request,
            Func<Task<DataResult<T>>> networkInvoker,
            CancellationToken cancellationToken = default);

        Task InvalidateCacheAsync(string? prefix = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Non-blocking file upload and download client with streaming, progress reporting, and atomic disk writes.
    /// </summary>
    public interface IFileTransferClient
    {
        Task<DataResult<string>> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default);
        Task<DataResult<FileInfo>> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default);
    }
}
