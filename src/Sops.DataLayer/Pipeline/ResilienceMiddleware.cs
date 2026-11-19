using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Resilience;

namespace Sops.DataLayer.Pipeline
{
    /// <summary>
    /// Middleware that applies circuit breaking, timeouts, exponential backoff retries, and unknown outcome handling for mutations.
    /// </summary>
    public sealed class ResilienceMiddleware : IRequestMiddleware
    {
        private readonly DataLayerOptions _options;
        private readonly CircuitBreaker _localCircuitBreaker;
        private readonly CircuitBreaker _cloudCircuitBreaker;
        private readonly ILogger<ResilienceMiddleware>? _logger;

        public ResilienceMiddleware(
            DataLayerOptions options,
            CircuitBreaker? localBreaker = null,
            CircuitBreaker? cloudBreaker = null,
            ILogger<ResilienceMiddleware>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _localCircuitBreaker = localBreaker ?? new CircuitBreaker(5, TimeSpan.FromSeconds(15));
            _cloudCircuitBreaker = cloudBreaker ?? new CircuitBreaker(5, TimeSpan.FromSeconds(30));
            _logger = logger;
        }

        public async Task<DataResult<T>> InvokeAsync<T>(EndpointDefinition endpoint, DataRequest request, RequestDelegate<T> next, CancellationToken cancellationToken)
        {
            var breaker = endpoint.Target == BackendTarget.LocalServer ? _localCircuitBreaker : _cloudCircuitBreaker;

            if (!breaker.AllowExecution())
            {
                _logger?.LogWarning("Circuit breaker is OPEN for {Target}. Rejecting request to {EndpointId}", endpoint.Target, endpoint.Id);
                var error = DataError.Unavailable($"Service {endpoint.Target} is temporarily unavailable due to previous failures.", request.Context.CorrelationId);
                return DataResult<T>.Failure(DataOutcome.ServerUnavailable, error);
            }

            int maxAttempts = 1;
            if (endpoint.RetryPolicy.AllowRetry && !IdempotencyPolicy.IsMutation(endpoint))
            {
                maxAttempts = 1 + Math.Min(endpoint.RetryPolicy.MaxRetries, _options.MaxRetryAttempts);
            }

            TimeSpan requestTimeout = request.Options.Timeout ?? (endpoint.Timeout > TimeSpan.Zero ? endpoint.Timeout : TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
            TimeSpan baseDelay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs);
            TimeSpan maxDelay = TimeSpan.FromMilliseconds(_options.RetryMaxDelayMs);

            DataResult<T>? lastResult = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (attempt > 1)
                {
                    var delay = RetryPolicy.ComputeDelay(attempt - 1, baseDelay, maxDelay);
                    _logger?.LogInformation("Retrying endpoint {EndpointId} (attempt {Attempt}/{MaxAttempts}) after {DelayMs}ms delay...", endpoint.Id, attempt, maxAttempts, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linkedCts.CancelAfter(requestTimeout);

                    try
                    {
                        lastResult = await next(endpoint, request, linkedCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return DataResult<T>.Cancelled(request.Context.CorrelationId);
                    }
                    catch (OperationCanceledException)
                    {
                        lastResult = DataResult<T>.Timeout(request.Context.CorrelationId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Unexpected error in request to {EndpointId}", endpoint.Id);
                        lastResult = DataResult<T>.Failure(
                            DataOutcome.Failure,
                            DataError.Create("UNEXPECTED_ERROR", ex.Message, request.Context.CorrelationId));
                    }
                }

                if (lastResult.IsSuccess)
                {
                    breaker.RecordSuccess();
                    return lastResult;
                }

                if (lastResult.Outcome == DataOutcome.ServerUnavailable)
                {
                    breaker.RecordFailure();
                }

                if (IdempotencyPolicy.ShouldPreserveUnknownOutcome(endpoint, lastResult.Outcome))
                {
                    _logger?.LogWarning("Mutation {EndpointId} returned uncertain outcome ({Outcome}). Preserving UnknownOutcome with IdempotencyKey {Key}.",
                        endpoint.Id, lastResult.Outcome, request.Context.IdempotencyKey);

                    return DataResult<T>.UnknownOutcome(
                        $"Transaction state is uncertain. Server response was lost or timed out. Use IdempotencyKey '{request.Context.IdempotencyKey}' to reconcile.",
                        request.Context.CorrelationId);
                }

                if (!RetryPolicy.IsRetryable(endpoint, lastResult.Outcome) || attempt == maxAttempts)
                {
                    break;
                }
            }

            return lastResult ?? DataResult<T>.Failure(DataOutcome.Failure, DataError.Create("UNKNOWN", "Request failed"));
        }
    }
}
