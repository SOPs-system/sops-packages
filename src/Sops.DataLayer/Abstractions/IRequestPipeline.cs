using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Abstractions
{
    /// <summary>
    /// Pipeline executor processing middleware and transports for a DataRequest.
    /// </summary>
    public interface IRequestPipeline
    {
        Task<DataResult<TResponse>> ExecuteAsync<TResponse>(DataRequest request, CancellationToken cancellationToken = default);
    }
}
