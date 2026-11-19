using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Pipeline;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class PipelineAndMiddlewareTests
    {
        [Fact]
        public async Task CorrelationMiddleware_Assigns_Unique_CorrelationId_And_TerminalId()
        {
            var options = new DataLayerOptions
            {
                TerminalId = "POS-TERMINAL-01",
                PharmacyId = "PHARM-999"
            };

            var middleware = new CorrelationMiddleware(options);
            var endpoint = new EndpointDefinition("test.ep", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/test");
            var request = new DataRequest("test.ep");

            DataRequest? captured = null;
            RequestDelegate<string> next = (ep, req, ct) =>
            {
                captured = req;
                return Task.FromResult(DataResult<string>.Success("ok"));
            };

            await middleware.InvokeAsync(endpoint, request, next, CancellationToken.None);

            Assert.NotNull(captured);
            Assert.False(string.IsNullOrWhiteSpace(captured!.Context.CorrelationId));
            Assert.Equal("POS-TERMINAL-01", captured.Context.TerminalId);
            Assert.Equal("PHARM-999", captured.Context.PharmacyId);
        }

        [Fact]
        public async Task ValidationMiddleware_Rejects_Financial_Mutation_Without_IdempotencyKey()
        {
            var middleware = new ValidationMiddleware();
            var endpoint = new EndpointDefinition("sales.complete", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/complete", sensitivity: SensitivityClassification.Financial);

            // Context without idempotency key
            var context = new RequestContext(idempotencyKey: null);
            var request = new DataRequest("sales.complete", new { saleId = "123" }, context: context);

            bool nextCalled = false;
            RequestDelegate<string> next = (ep, req, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(DataResult<string>.Success("ok"));
            };

            var result = await middleware.InvokeAsync(endpoint, request, next, CancellationToken.None);

            Assert.False(nextCalled);
            Assert.False(result.IsSuccess);
            Assert.Equal(DataOutcome.ValidationFailed, result.Outcome);
            Assert.Equal("MISSING_IDEMPOTENCY_KEY", result.Error?.Code);
        }

        [Fact]
        public async Task ValidationMiddleware_Passes_Financial_Mutation_With_IdempotencyKey()
        {
            var middleware = new ValidationMiddleware();
            var endpoint = new EndpointDefinition("sales.complete", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/complete", sensitivity: SensitivityClassification.Financial);

            var context = new RequestContext(idempotencyKey: "idemp_abc123");
            var request = new DataRequest("sales.complete", new { saleId = "123" }, context: context);

            bool nextCalled = false;
            RequestDelegate<string> next = (ep, req, ct) =>
            {
                nextCalled = true;
                return Task.FromResult(DataResult<string>.Success("ok"));
            };

            var result = await middleware.InvokeAsync(endpoint, request, next, CancellationToken.None);

            Assert.True(nextCalled);
            Assert.True(result.IsSuccess);
        }
    }
}
