using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Transport
{
    /// <summary>
    /// Transport abstraction responsible for sending an HTTP or RPC request to a remote endpoint.
    /// </summary>
    public interface ITransport
    {
        Task<DataResult<TResponse>> SendAsync<TResponse>(
            EndpointDefinition endpoint,
            DataRequest request,
            Uri resolvedUri,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Factory for selecting the appropriate transport (REST HTTP vs JSON-RPC).
    /// </summary>
    public interface ITransportFactory
    {
        ITransport GetTransport(EndpointDefinition endpoint);
    }
}
