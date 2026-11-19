using System;
using System.Net.Http;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Endpoints;

namespace Sops.DataLayer.Transport
{
    /// <summary>
    /// Factory for resolving the appropriate transport (REST HTTP vs JSON-RPC).
    /// </summary>
    public sealed class TransportFactory : ITransportFactory
    {
        private readonly ITransport _httpTransport;
        private readonly ITransport _rpcTransport;

        public TransportFactory(HttpClient httpClient, ISessionManager sessionManager)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
            if (sessionManager == null) throw new ArgumentNullException(nameof(sessionManager));

            _httpTransport = new HttpTransport(httpClient, sessionManager);
            _rpcTransport = new RpcTransport(httpClient, sessionManager);
        }

        public TransportFactory(ITransport httpTransport, ITransport rpcTransport)
        {
            _httpTransport = httpTransport ?? throw new ArgumentNullException(nameof(httpTransport));
            _rpcTransport = rpcTransport ?? throw new ArgumentNullException(nameof(rpcTransport));
        }

        public ITransport GetTransport(EndpointDefinition endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            switch (endpoint.Transport)
            {
                case TransportMode.Rpc:
                    return _rpcTransport;
                case TransportMode.HttpRest:
                case TransportMode.ServerSentEvents:
                default:
                    return _httpTransport;
            }
        }
    }
}
