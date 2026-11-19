using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Pipeline
{
    /// <summary>
    /// Validates requests before sending them across the network.
    /// </summary>
    public sealed class ValidationMiddleware : IRequestMiddleware
    {
        public Task<DataResult<T>> InvokeAsync<T>(EndpointDefinition endpoint, DataRequest request, RequestDelegate<T> next, CancellationToken cancellationToken)
        {
            if (endpoint == null)
            {
                var error = DataError.Create("MISSING_ENDPOINT", "Endpoint definition cannot be null.");
                return Task.FromResult(DataResult<T>.Failure(DataOutcome.ValidationFailed, error));
            }

            if (request == null)
            {
                var error = DataError.Create("MISSING_REQUEST", "DataRequest cannot be null.");
                return Task.FromResult(DataResult<T>.Failure(DataOutcome.ValidationFailed, error));
            }

            if ((endpoint.RequiresIdempotencyKey || endpoint.IsFinancialMutation || endpoint.Sensitivity == SensitivityClassification.Financial) && string.IsNullOrWhiteSpace(request.Context.IdempotencyKey))
            {
                var error = DataError.Create("MISSING_IDEMPOTENCY_KEY", $"Mutation operation on endpoint '{endpoint.Id}' requires an idempotency key.");
                return Task.FromResult(DataResult<T>.Failure(DataOutcome.ValidationFailed, error));
            }

            return next(endpoint, request, cancellationToken);
        }
    }
}
