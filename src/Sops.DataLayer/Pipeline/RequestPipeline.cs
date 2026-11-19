using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Resilience;
using Sops.DataLayer.Transport;

namespace Sops.DataLayer.Pipeline
{
    /// <summary>
    /// Implements the layered request processing pipeline for Sops.DataLayer.
    /// </summary>
    public sealed class RequestPipeline : IRequestPipeline
    {
        private readonly IEndpointRegistry _endpointRegistry;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ITransportFactory _transportFactory;
        private readonly IOfflineReadCoordinator? _offlineCoordinator;
        private readonly List<IRequestMiddleware> _middlewares = new List<IRequestMiddleware>();
        private readonly ILogger<RequestPipeline>? _logger;

        public RequestPipeline(
            IEndpointRegistry endpointRegistry,
            IEndpointResolver endpointResolver,
            ITransportFactory transportFactory,
            IOfflineReadCoordinator? offlineCoordinator = null,
            IEnumerable<IRequestMiddleware>? middlewares = null,
            ILogger<RequestPipeline>? logger = null)
        {
            _endpointRegistry = endpointRegistry ?? throw new ArgumentNullException(nameof(endpointRegistry));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _offlineCoordinator = offlineCoordinator;
            _logger = logger;

            if (middlewares != null)
            {
                _middlewares.AddRange(middlewares);
            }
        }

        public void AddMiddleware(IRequestMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Add(middleware);
        }

        public Task<DataResult<TResponse>> ExecuteAsync<TResponse>(DataRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!_endpointRegistry.TryGet(request.EndpointId, out var endpoint))
            {
                var error = DataError.Create("ENDPOINT_NOT_FOUND", $"Endpoint '{request.EndpointId}' is not registered.");
                return Task.FromResult(DataResult<TResponse>.Failure(DataOutcome.NotFound, error));
            }

            return ExecuteAsync<TResponse>(endpoint!, request, cancellationToken);
        }

        public async Task<DataResult<T>> ExecuteAsync<T>(EndpointDefinition endpoint, DataRequest request, CancellationToken cancellationToken)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Func<Task<DataResult<T>>> pipelineInvoker = () => ExecuteChainAsync<T>(endpoint, request, cancellationToken);

            if (_offlineCoordinator != null)
            {
                var result = await _offlineCoordinator.ExecuteWithCachePolicyAsync(endpoint, request, pipelineInvoker, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess && IdempotencyPolicy.IsMutation(endpoint))
                {
                    // Invalidate cache after successful mutation
                    await _offlineCoordinator.InvalidateCacheAsync(endpoint.Id, cancellationToken).ConfigureAwait(false);
                }
                return result;
            }

            return await pipelineInvoker().ConfigureAwait(false);
        }

        private async Task<DataResult<T>> ExecuteChainAsync<T>(EndpointDefinition endpoint, DataRequest request, CancellationToken cancellationToken)
        {
            RequestDelegate<T> chain = (ep, req, ct) => ExecuteTerminalTransportAsync<T>(ep, req, ct);

            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var mw = _middlewares[i];
                var next = chain;
                chain = (ep, req, ct) => mw.InvokeAsync(ep, req, next, ct);
            }

            return await chain(endpoint, request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<DataResult<T>> ExecuteTerminalTransportAsync<T>(EndpointDefinition endpoint, DataRequest request, CancellationToken cancellationToken)
        {
            var resolvedUri = _endpointResolver.ResolveUri(endpoint, request.Options.QueryParameters);
            var transport = _transportFactory.GetTransport(endpoint);

            _logger?.LogDebug("Sending {HttpMethod} request to {ResolvedUri} via {Transport}",
                endpoint.HttpMethod, resolvedUri, transport.GetType().Name);

            return await transport.SendAsync<T>(endpoint, request, resolvedUri, cancellationToken).ConfigureAwait(false);
        }
    }
}
