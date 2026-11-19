using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Sops.Database.Resilience
{
    /// <summary>
    /// Executes database operations under a bounded retry policy with cancellation awareness and idempotency enforcement.
    /// Non-idempotent operations are never retried automatically.
    /// </summary>
    public static class DatabaseRetryExecutor
    {
        /// <summary>
        /// Executes an asynchronous database operation under the specified retry policy.
        /// If <paramref name="isIdempotent"/> is <c>false</c>, the operation is attempted exactly once.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The asynchronous operation to execute.</param>
        /// <param name="policy">The retry policy defining bounded attempts and delays.</param>
        /// <param name="isIdempotent">Whether the operation is idempotent and safe to retry.</param>
        /// <param name="logger">Optional logger for logging retry attempts.</param>
        /// <param name="operationName">Descriptive name for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the executed operation.</returns>
        public static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            DatabaseRetryPolicy policy,
            bool isIdempotent,
            ILogger? logger = null,
            string operationName = "DatabaseOperation",
            CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            policy = policy ?? DatabaseRetryPolicy.None;

            // Strict safety gate: Non-idempotent operations (such as writes or uncommitted transactions)
            // or policies with MaxAttempts <= 1 must never be retried automatically.
            if (!isIdempotent || policy.MaxAttempts <= 1)
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }

            int attempt = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation must propagate immediately without retry
                    throw;
                }
                catch (Exception ex)
                {
                    bool isTransient = policy.TransientDetector(ex);
                    if (!isTransient || attempt >= policy.MaxAttempts)
                    {
                        // Final failure or non-transient error: rethrow original exception untouched
                        throw;
                    }

                    // Compute exponential backoff delay capped at MaxDelay
                    double delayFactor = Math.Pow(policy.BackoffMultiplier, attempt - 1);
                    long delayMs = (long)(policy.InitialDelay.TotalMilliseconds * delayFactor);
                    if (delayMs > policy.MaxDelay.TotalMilliseconds)
                    {
                        delayMs = (long)policy.MaxDelay.TotalMilliseconds;
                    }

                    TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);

                    logger?.LogWarning(
                        "Transient failure during {OperationName} (attempt {Attempt}/{MaxAttempts}). Retrying in {DelayMs}ms. Reason: {ErrorType}",
                        operationName,
                        attempt,
                        policy.MaxAttempts,
                        delayMs,
                        ex.GetType().Name);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Executes an asynchronous database action under the specified retry policy.
        /// If <paramref name="isIdempotent"/> is <c>false</c>, the action is attempted exactly once.
        /// </summary>
        /// <param name="action">The asynchronous action to execute.</param>
        /// <param name="policy">The retry policy defining bounded attempts and delays.</param>
        /// <param name="isIdempotent">Whether the action is idempotent and safe to retry.</param>
        /// <param name="logger">Optional logger for logging retry attempts.</param>
        /// <param name="operationName">Descriptive name for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing completion of the action.</returns>
        public static async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            DatabaseRetryPolicy policy,
            bool isIdempotent,
            ILogger? logger = null,
            string operationName = "DatabaseOperation",
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            await ExecuteAsync<bool>(
                async ct =>
                {
                    await action(ct).ConfigureAwait(false);
                    return true;
                },
                policy,
                isIdempotent,
                logger,
                operationName,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
