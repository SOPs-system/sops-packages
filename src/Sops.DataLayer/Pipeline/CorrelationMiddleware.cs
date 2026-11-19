using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Pipeline
{
    /// <summary>
    /// Ensures every request has a unique correlation ID and proper terminal/pharmacy IDs.
    /// </summary>
    public sealed class CorrelationMiddleware : IRequestMiddleware
    {
        private readonly DataLayerOptions _options;

        public CorrelationMiddleware(DataLayerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<DataResult<T>> InvokeAsync<T>(EndpointDefinition endpoint, DataRequest request, RequestDelegate<T> next, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var context = request.Context;
            bool mutated = false;

            string correlationId = context.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
                mutated = true;
            }

            string terminalId = context.TerminalId;
            if ((string.IsNullOrWhiteSpace(terminalId) || terminalId == Environment.MachineName) && !string.IsNullOrWhiteSpace(_options.TerminalId))
            {
                terminalId = _options.TerminalId;
                mutated = true;
            }

            string? pharmacyId = context.PharmacyId;
            if (string.IsNullOrWhiteSpace(pharmacyId) && !string.IsNullOrWhiteSpace(_options.PharmacyId))
            {
                pharmacyId = _options.PharmacyId;
                mutated = true;
            }

            string? idempotencyKey = context.IdempotencyKey;
            if (string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(request.Options.IdempotencyKey))
            {
                idempotencyKey = request.Options.IdempotencyKey;
                mutated = true;
            }

            if (mutated)
            {
                request.Context = context.WithOverrides(
                    correlationId: correlationId,
                    idempotencyKey: idempotencyKey,
                    terminalId: terminalId,
                    pharmacyId: pharmacyId);
            }

            return next(endpoint, request, cancellationToken);
        }
    }
}
