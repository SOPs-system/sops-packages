using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Abstractions
{
    /// <summary>
    /// Core low-level client executing typed DataRequests against registered endpoints.
    /// </summary>
    public interface IDataLayerClient
    {
        /// <summary>Executes a typed request returning a typed result.</summary>
        Task<DataResult<TResponse>> ExecuteAsync<TResponse>(DataRequest request, CancellationToken cancellationToken = default);

        /// <summary>Executes a request without a typed response payload.</summary>
        Task<DataResult> ExecuteAsync(DataRequest request, CancellationToken cancellationToken = default);
    }
}
