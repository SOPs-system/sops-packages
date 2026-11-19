using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Client
{
    /// <summary>
    /// Core client executing typed DataRequests against registered endpoints.
    /// </summary>
    public sealed class DataLayerClient : IDataLayerClient
    {
        private readonly IEndpointRegistry _endpointRegistry;
        private readonly IRequestPipeline _pipeline;
        private readonly ILogger<DataLayerClient>? _logger;

        public DataLayerClient(
            IEndpointRegistry endpointRegistry,
            IRequestPipeline pipeline,
            ILogger<DataLayerClient>? logger = null)
        {
            _endpointRegistry = endpointRegistry ?? throw new ArgumentNullException(nameof(endpointRegistry));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _logger = logger;
        }

        public async Task<DataResult<TResponse>> ExecuteAsync<TResponse>(DataRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!_endpointRegistry.TryGet(request.EndpointId, out _))
            {
                return DataResult<TResponse>.NotFound($"Endpoint '{request.EndpointId}' is not registered in EndpointRegistry.");
            }

            return await _pipeline.ExecuteAsync<TResponse>(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<DataResult> ExecuteAsync(DataRequest request, CancellationToken cancellationToken = default)
        {
            var genericResult = await ExecuteAsync<object>(request, cancellationToken).ConfigureAwait(false);
            if (genericResult.IsSuccess)
            {
                return DataResult.Success(genericResult.Metadata);
            }

            return DataResult.Failure(genericResult.Outcome, genericResult.Error ?? DataError.Create("FAILURE", "Operation failed"), genericResult.Metadata);
        }
    }
}
