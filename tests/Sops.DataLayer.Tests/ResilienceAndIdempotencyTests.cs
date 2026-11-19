using System;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Resilience;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class ResilienceAndIdempotencyTests
    {
        [Fact]
        public void RetryPolicy_Computes_Exponential_Backoff_With_Jitter()
        {
            var initial = TimeSpan.FromMilliseconds(100);
            var max = TimeSpan.FromMilliseconds(5000);

            var delay1 = RetryPolicy.ComputeDelay(1, initial, max);
            var delay2 = RetryPolicy.ComputeDelay(2, initial, max);
            var delay3 = RetryPolicy.ComputeDelay(3, initial, max);

            Assert.True(delay1 >= TimeSpan.FromMilliseconds(50));
            Assert.True(delay1 <= TimeSpan.FromMilliseconds(150));

            Assert.True(delay2 >= TimeSpan.FromMilliseconds(100));
            Assert.True(delay2 <= TimeSpan.FromMilliseconds(300));

            Assert.True(delay3 <= max);
        }

        [Fact]
        public void CircuitBreaker_Transitions_Closed_Open_HalfOpen()
        {
            var breaker = new CircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromMilliseconds(50));

            Assert.Equal(CircuitState.Closed, breaker.State);
            Assert.True(breaker.AllowExecution());

            breaker.RecordFailure();
            Assert.Equal(CircuitState.Closed, breaker.State);
            Assert.True(breaker.AllowExecution());

            breaker.RecordFailure();
            Assert.Equal(CircuitState.Open, breaker.State);
            Assert.False(breaker.AllowExecution());

            // Wait for break duration to lapse
            System.Threading.Thread.Sleep(70);

            Assert.Equal(CircuitState.HalfOpen, breaker.State);
            Assert.True(breaker.AllowExecution());

            // Success resets to Closed
            breaker.RecordSuccess();
            Assert.Equal(CircuitState.Closed, breaker.State);
        }

        [Fact]
        public void IdempotencyPolicy_Identifies_Mutations()
        {
            var getEp = new EndpointDefinition("drugs.get", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/api/drugs");
            var postEp = new EndpointDefinition("sales.complete", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/complete");

            Assert.False(IdempotencyPolicy.IsMutation(getEp));
            Assert.True(IdempotencyPolicy.IsMutation(postEp));

            Assert.False(IdempotencyPolicy.ShouldPreserveUnknownOutcome(getEp, DataOutcome.Timeout));
            Assert.True(IdempotencyPolicy.ShouldPreserveUnknownOutcome(postEp, DataOutcome.Timeout));
            Assert.True(IdempotencyPolicy.ShouldPreserveUnknownOutcome(postEp, DataOutcome.ServerUnavailable));
        }
    }
}
