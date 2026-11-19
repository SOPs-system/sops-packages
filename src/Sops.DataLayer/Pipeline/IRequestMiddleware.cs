using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Pipeline
{
    /// <summary>
    /// Next delegate in the request pipeline.
    /// </summary>
    public delegate Task<DataResult<T>> RequestDelegate<T>(EndpointDefinition endpoint, DataRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Pluggable middleware interceptor in the request pipeline.
    /// </summary>
    public interface IRequestMiddleware
    {
        Task<DataResult<T>> InvokeAsync<T>(EndpointDefinition endpoint, DataRequest request, RequestDelegate<T> next, CancellationToken cancellationToken);
    }
}
