using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.Cache.Memory;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Offline;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class OfflineAndCacheTests
    {
        [Fact]
        public async Task OfflineReadCoordinator_Stores_And_Serves_Cached_Data()
        {
            var memoryCache = new MemoryCacheService();
            var options = new DataLayerOptions { EnableCaching = true };
            var coordinator = new OfflineReadCoordinator(memoryCache, options);

            var endpoint = new EndpointDefinition(
                "drugs.search",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/drugs/search",
                isIdempotent: true,
                cachePolicy: EndpointCachePolicy.Minutes(10),
                offlinePolicy: EndpointOfflinePolicy.CacheFallback);

            var request = new DataRequest("drugs.search", new { q = "aspirin" });

            int networkCallCount = 0;
            Func<Task<DataResult<string>>> networkInvoker = () =>
            {
                networkCallCount++;
                return Task.FromResult(DataResult<string>.Success("Aspirin 100mg"));
            };

            // 1st call: network invocation and cached
            var res1 = await coordinator.ExecuteWithCachePolicyAsync(endpoint, request, networkInvoker);
            Assert.True(res1.IsSuccess);
            Assert.Equal("Aspirin 100mg", res1.Data);
            Assert.Equal(1, networkCallCount);

            // 2nd call: served from memory cache (network not invoked)
            var res2 = await coordinator.ExecuteWithCachePolicyAsync(endpoint, request, networkInvoker);
            Assert.True(res2.IsSuccess);
            Assert.Equal("Aspirin 100mg", res2.Data);
            Assert.Equal(1, networkCallCount);
            Assert.Equal(DataSourceOrigin.MemoryCache, res2.Metadata?.Source);
        }

        [Fact]
        public async Task OfflineReadCoordinator_Falls_Back_To_Stale_Cache_During_Network_Outage()
        {
            var memoryCache = new MemoryCacheService();
            var options = new DataLayerOptions { EnableCaching = true };
            var coordinator = new OfflineReadCoordinator(memoryCache, options);

            var endpoint = new EndpointDefinition(
                "drugs.getbybarcode",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/drugs/barcode",
                isIdempotent: true,
                cachePolicy: EndpointCachePolicy.Minutes(5),
                offlinePolicy: EndpointOfflinePolicy.CacheFallback);

            var request = new DataRequest("drugs.getbybarcode", new { barcode = "6221234567890" });

            // Seed cache with initial online call
            await coordinator.ExecuteWithCachePolicyAsync(endpoint, request, () => Task.FromResult(DataResult<string>.Success("Panadol Extra")));

            // Now simulate local server disconnection (networkInvoker returns ServerUnavailable)
            Func<Task<DataResult<string>>> offlineInvoker = () =>
            {
                return Task.FromResult(DataResult<string>.Failure(
                    DataOutcome.ServerUnavailable,
                    DataError.Unavailable("Local server connection lost")));
            };

            // Coordinator should catch failure and fall back to stale cache
            var offlineRes = await coordinator.ExecuteWithCachePolicyAsync(endpoint, request, offlineInvoker);

            Assert.True(offlineRes.IsSuccess);
            Assert.Equal("Panadol Extra", offlineRes.Data);
            Assert.Equal(DataSourceOrigin.MemoryCache, offlineRes.Metadata?.Source);
        }

        [Fact]
        public async Task OfflineReadCoordinator_CacheOnly_Returns_NotFound_On_Miss()
        {
            var memoryCache = new MemoryCacheService();
            var options = new DataLayerOptions { EnableCaching = true };
            var coordinator = new OfflineReadCoordinator(memoryCache, options);

            var endpoint = new EndpointDefinition(
                "drugs.search",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/drugs/search",
                cachePolicy: EndpointCachePolicy.Minutes(5));

            var opt = new RequestOptions { CacheOnly = true };
            var request = new DataRequest("drugs.search", new { q = "nonexistent" }, opt);

            var result = await coordinator.ExecuteWithCachePolicyAsync<string>(endpoint, request, () => Task.FromResult(DataResult<string>.Success("data")));

            Assert.False(result.IsSuccess);
            Assert.Equal(DataOutcome.NotFound, result.Outcome);
        }
    }
}
