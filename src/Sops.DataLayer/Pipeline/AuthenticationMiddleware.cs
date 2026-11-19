using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Authentication;
using Sops.DataLayer.Authorization;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Pipeline
{
    /// <summary>
    /// Middleware that verifies permissions, attaches authentication tokens, and handles transparent single-flight refresh on 401.
    /// </summary>
    public sealed class AuthenticationMiddleware : IRequestMiddleware
    {
        private readonly ISessionManager _sessionManager;
        private readonly IAuthorizationStateProvider _authzProvider;
        private readonly TokenRefreshCoordinator? _refreshCoordinator;
        private readonly ILogger<AuthenticationMiddleware>? _logger;

        public AuthenticationMiddleware(
            ISessionManager sessionManager,
            IAuthorizationStateProvider authzProvider,
            TokenRefreshCoordinator? refreshCoordinator = null,
            ILogger<AuthenticationMiddleware>? logger = null)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _authzProvider = authzProvider ?? throw new ArgumentNullException(nameof(authzProvider));
            _refreshCoordinator = refreshCoordinator;
            _logger = logger;
        }

        public async Task<DataResult<T>> InvokeAsync<T>(EndpointDefinition endpoint, DataRequest request, RequestDelegate<T> next, CancellationToken cancellationToken)
        {
            if (endpoint.RequiresAuthentication)
            {
                if (_authzProvider is PermissionClient permClient && !permClient.CanAccess(endpoint))
                {
                    _logger?.LogWarning("Access forbidden for endpoint {EndpointId}", endpoint.Id);
                    return DataResult<T>.Forbidden("Current user lacks permission for endpoint " + endpoint.Id, request.Context.CorrelationId);
                }

                if (!_sessionManager.IsAuthenticated)
                {
                    _logger?.LogWarning("Endpoint {EndpointId} requires auth but no session is active", endpoint.Id);
                    return DataResult<T>.Unauthorized("Authentication required for endpoint " + endpoint.Id, request.Context.CorrelationId);
                }
            }

            var result = await next(endpoint, request, cancellationToken).ConfigureAwait(false);

            if (result.Outcome == DataOutcome.Unauthorized && endpoint.RequiresAuthentication && _refreshCoordinator != null)
            {
                _logger?.LogInformation("Received 401 Unauthorized for {EndpointId}. Attempting single-flight token refresh...", endpoint.Id);
                var refreshResult = await _refreshCoordinator.RefreshTokenAsync(cancellationToken).ConfigureAwait(false);

                if (refreshResult.IsSuccess && refreshResult.Data != null)
                {
                    _logger?.LogInformation("Retrying request to {EndpointId} after token refresh", endpoint.Id);
                    result = await next(endpoint, request, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger?.LogWarning("Token refresh failed. Preserving original Unauthorized outcome.");
                }
            }

            return result;
        }
    }
}
