using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.Database.Resilience;
using Xunit;

namespace Sops.Database.Tests
{
    public class RetryTests
    {
        [Fact]
        public async Task IdempotentOperation_RetriesOnTransientError_AndSucceeds()
        {
            var policy = new DatabaseRetryPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(10),
                maxDelay: TimeSpan.FromMilliseconds(50),
                backoffMultiplier: 2.0);

            int invocationCount = 0;

            int result = await DatabaseRetryExecutor.ExecuteAsync(
                async ct =>
                {
                    invocationCount++;
                    if (invocationCount < 3)
                    {
                        throw new TimeoutException("Database operation timed out.");
                    }
                    await Task.Yield();
                    return 42;
                },
                policy,
                isIdempotent: true);

            Assert.Equal(42, result);
            Assert.Equal(3, invocationCount);
        }

        [Fact]
        public async Task NonIdempotentOperation_NeverRetries_EvenOnTransientError()
        {
            var policy = new DatabaseRetryPolicy(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromMilliseconds(10),
                maxDelay: TimeSpan.FromMilliseconds(50));

            int invocationCount = 0;

            var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
                DatabaseRetryExecutor.ExecuteAsync(
                    async ct =>
                    {
                        invocationCount++;
                        await Task.Yield();
                        throw new TimeoutException("Timeout during non-idempotent write.");
                    },
                    policy,
                    isIdempotent: false)); // Non-idempotent write!

            Assert.Equal(1, invocationCount); // Must be attempted exactly ONCE!
            Assert.Contains("Timeout during non-idempotent write", ex.Message);
        }

        [Fact]
        public async Task NonTransientException_NeverRetried()
        {
            var policy = new DatabaseRetryPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(10));

            int invocationCount = 0;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DatabaseRetryExecutor.ExecuteAsync(
                    async ct =>
                    {
                        invocationCount++;
                        await Task.Yield();
                        throw new InvalidOperationException("Fatal database constraint violation.");
                    },
                    policy,
                    isIdempotent: true));

            Assert.Equal(1, invocationCount);
            Assert.Contains("Fatal database constraint violation", ex.Message);
        }

        [Fact]
        public async Task MaxAttemptsExhausted_RethrowsOriginalException()
        {
            var policy = new DatabaseRetryPolicy(
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(5));

            int invocationCount = 0;

            var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
                DatabaseRetryExecutor.ExecuteAsync(
                    async ct =>
                    {
                        invocationCount++;
                        await Task.Yield();
                        throw new TimeoutException("Persistent transient network failure.");
                    },
                    policy,
                    isIdempotent: true));

            Assert.Equal(3, invocationCount);
            Assert.Contains("Persistent transient network failure", ex.Message);
        }

        [Fact]
        public async Task CancellationToken_AbortsRetriesImmediately()
        {
            var policy = new DatabaseRetryPolicy(
                maxAttempts: 5,
                initialDelay: TimeSpan.FromMilliseconds(200));

            using var cts = new CancellationTokenSource();
            int invocationCount = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                DatabaseRetryExecutor.ExecuteAsync(
                    async ct =>
                    {
                        invocationCount++;
                        cts.Cancel(); // Cancel token on first failure
                        await Task.Yield();
                        throw new TimeoutException("Timeout failure.");
                    },
                    policy,
                    isIdempotent: true,
                    cancellationToken: cts.Token));

            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void RetryPolicy_EnforcesBoundsOnAttempts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DatabaseRetryPolicy(maxAttempts: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DatabaseRetryPolicy(maxAttempts: 11));

            var validPolicy = new DatabaseRetryPolicy(maxAttempts: 10);
            Assert.Equal(10, validPolicy.MaxAttempts);
        }
    }
}
