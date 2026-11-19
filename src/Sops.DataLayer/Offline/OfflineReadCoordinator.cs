using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Cache.Abstractions;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Resilience;

namespace Sops.DataLayer.Offline
{
    /// <summary>
    /// Coordinates offline read strategies, caching, and stale result presentation using ICacheService.
    /// Strictly isolates cache partitions by Pharmacy, Branch, and User, and prohibits implicit offline mutations.
    /// </summary>
    public sealed class OfflineReadCoordinator : IOfflineReadCoordinator
    {
        private readonly ICacheService _cacheService;
        private readonly DataLayerOptions _options;
        private readonly ILogger<OfflineReadCoordinator>? _logger;

        public OfflineReadCoordinator(
            ICacheService cacheService,
            DataLayerOptions options,
            ILogger<OfflineReadCoordinator>? logger = null)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task<DataResult<T>> ExecuteWithCachePolicyAsync<T>(
            EndpointDefinition endpoint,
            DataRequest request,
            Func<Task<DataResult<T>>> networkInvoker,
            CancellationToken cancellationToken = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (networkInvoker == null) throw new ArgumentNullException(nameof(networkInvoker));

            // Strictly prohibit offline writes for financial and transactional mutations
            if (IdempotencyPolicy.IsMutation(endpoint) && endpoint.OfflinePolicy == EndpointOfflinePolicy.OnlineOnly)
            {
                if (request.Options.CacheOnly)
                {
                    return DataResult<T>.Failure(
                        DataOutcome.ServerUnavailable,
                        DataError.Unavailable($"Mutations on endpoint '{endpoint.Id}' cannot be executed offline.", request.Context.CorrelationId));
                }
            }

            if (!_options.EnableCaching || !endpoint.CachePolicy.IsCacheable)
            {
                return await networkInvoker().ConfigureAwait(false);
            }

            string cacheKey = BuildCacheKey(endpoint, request);

            if (request.Options.CacheOnly)
            {
                var cached = await TryGetCachedAsync<T>(cacheKey, cancellationToken).ConfigureAwait(false);
                if (cached != null) return cached;

                return DataResult<T>.NotFound("Requested resource is not available in local offline cache.", request.Context.CorrelationId);
            }

            if (!request.Options.ForceRefresh)
            {
                var cached = await TryGetCachedAsync<T>(cacheKey, cancellationToken).ConfigureAwait(false);
                if (cached != null && cached.Metadata != null && !cached.Metadata.IsStale)
                {
                    return cached;
                }
            }

            // Execute network call
            DataResult<T> networkResult;
            try
            {
                networkResult = await networkInvoker().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error invoking network request for {EndpointId}", endpoint.Id);
                networkResult = DataResult<T>.Failure(DataOutcome.Failure, DataError.Create("NETWORK_ERROR", ex.Message, request.Context.CorrelationId));
            }

            if (networkResult.IsSuccess && networkResult.Data != null)
            {
                TimeSpan ttl = endpoint.CachePolicy.Duration > TimeSpan.Zero ? endpoint.CachePolicy.Duration : TimeSpan.FromMinutes(_options.DefaultCacheTtlMinutes);
                await SetCacheAsync(cacheKey, networkResult.Data, ttl, cancellationToken).ConfigureAwait(false);
                return networkResult;
            }

            // Fallback to stale cache if network failed and endpoint allows cache fallback (strictly forbidden for financial mutations)
            if (!networkResult.IsSuccess && endpoint.OfflinePolicy == EndpointOfflinePolicy.CacheFallback && !endpoint.IsFinancialMutation)
            {
                var stale = await TryGetCachedAsync<T>(cacheKey, cancellationToken).ConfigureAwait(false);
                if (stale != null)
                {
                    _logger?.LogInformation("Serving stale cache for {EndpointId} due to network outcome {Outcome}", endpoint.Id, networkResult.Outcome);
                    return stale;
                }
            }

            return networkResult;
        }

        public async Task InvalidateCacheAsync(string? prefix = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return;
            try
            {
                await _cacheService.RemoveAsync(prefix!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to invalidate cache for key {Prefix}", prefix);
            }
        }

        private async Task<DataResult<T>?> TryGetCachedAsync<T>(string key, CancellationToken cancellationToken)
        {
            try
            {
                var cachedResult = await _cacheService.GetAsync<CachedItemWrapper<T>>(key, cancellationToken).ConfigureAwait(false);
                if (cachedResult.IsHit && cachedResult.Value != null)
                {
                    var wrapper = cachedResult.Value!;
                    bool isStale = DateTimeOffset.UtcNow > wrapper.ExpiresAt;

                    var metadata = ResponseMetadata.Cached(wrapper.CachedAt, isStale);
                    return DataResult<T>.Success(wrapper.Data, metadata);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error reading cache for key {Key}", key);
            }

            return null;
        }

        private async Task SetCacheAsync<T>(string key, T data, TimeSpan ttl, CancellationToken cancellationToken)
        {
            var wrapper = new CachedItemWrapper<T>
            {
                Data = data,
                CachedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
            };

            var entryOptions = new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            try
            {
                await _cacheService.SetAsync(key, wrapper, entryOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to write cache for key {Key}", key);
            }
        }

        private static string BuildCacheKey(EndpointDefinition endpoint, DataRequest request)
        {
            string pharmacy = !string.IsNullOrWhiteSpace(request.Context.PharmacyId) ? request.Context.PharmacyId! : "default";
            string branch = !string.IsNullOrWhiteSpace(request.Context.BranchId) ? request.Context.BranchId! : "main";
            string userScope = endpoint.Sensitivity == SensitivityClassification.Operational || endpoint.Sensitivity == SensitivityClassification.SecuritySensitive
                ? (!string.IsNullOrWhiteSpace(request.Context.UserId) ? request.Context.UserId! : "anon")
                : "shared";

            string key = $"sops:dl:p:{pharmacy}:b:{branch}:u:{userScope}:{endpoint.Id}";

            if (request.Body != null)
            {
                try
                {
                    key += ":" + JsonSerializer.Serialize(request.Body);
                }
                catch
                {
                    key += ":" + request.Body.GetHashCode();
                }
            }
            return key;
        }

        private class CachedItemWrapper<T>
        {
            public T Data { get; set; } = default!;
            public DateTimeOffset CachedAt { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }
    }
}
