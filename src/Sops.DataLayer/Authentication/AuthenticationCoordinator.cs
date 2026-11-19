using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Authentication
{
    /// <summary>
    /// Coordinates authentication, login, refresh, logout, and credential lifecycle.
    /// </summary>
    public sealed class AuthenticationCoordinator : IAuthenticationService
    {
        private readonly IDataLayerClient _client;
        private readonly ISessionManager _sessionManager;
        private readonly ISecureCredentialStore _credentialStore;
        private readonly TokenRefreshCoordinator _refreshCoordinator;
        private readonly ILogger<AuthenticationCoordinator>? _logger;

        private AuthenticationState _state = AuthenticationState.Unauthenticated;
        public AuthenticationState State => _state;
        public SessionInfo? CurrentSession => _sessionManager.CurrentSession;
        public string? CurrentAccessToken => _sessionManager.CurrentAccessToken;
        public string? CloudAccessToken => _sessionManager.CloudAccessToken;

        public event EventHandler<AuthenticationState>? StateChanged;

        public AuthenticationCoordinator(
            IDataLayerClient client,
            ISessionManager sessionManager,
            ISecureCredentialStore credentialStore,
            ILogger<AuthenticationCoordinator>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _logger = logger;

            _refreshCoordinator = new TokenRefreshCoordinator(
                sessionManager,
                credentialStore,
                (refreshToken, ct) => ExecuteRefreshNetworkCallAsync(refreshToken, ct),
                logger != null ? new LoggerFactory().CreateLogger<TokenRefreshCoordinator>() : null);
        }

        public async Task<DataResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            SetState(AuthenticationState.Authenticating);

            var dataRequest = new DataRequest("auth.login", request);
            var result = await _client.ExecuteAsync<LoginResponse>(dataRequest, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && result.Data != null)
            {
                _sessionManager.SetSession(result.Data);
                SetState(AuthenticationState.Authenticated);
            }
            else
            {
                SetState(AuthenticationState.Unauthenticated);
            }

            return result;
        }

        public async Task<DataResult<string>> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            var result = await _refreshCoordinator.RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && result.Data != null)
            {
                return DataResult<string>.Success(result.Data.AccessToken, result.Metadata);
            }

            return DataResult<string>.Failure(result.Outcome, result.Error ?? DataError.Create("REFRESH_FAILED", "Failed to refresh token"), result.Metadata);
        }

        public async Task<DataResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var dataRequest = new DataRequest("auth.logout");
                await _client.ExecuteAsync(dataRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error while notifying backend of logout");
            }
            finally
            {
                _sessionManager.ClearSession();
                SetState(AuthenticationState.Unauthenticated);
            }

            return DataResult.Success();
        }

        public async Task<DataResult<SessionInfo>> RestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            string? savedToken = await _credentialStore.GetTokenAsync("current_session_token", cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(savedToken))
            {
                SetState(AuthenticationState.Unauthenticated);
                return DataResult<SessionInfo>.Unauthorized("No stored credentials found.");
            }

            var dataRequest = new DataRequest("auth.me");
            var result = await _client.ExecuteAsync<SessionInfo>(dataRequest, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && result.Data != null)
            {
                var loginResp = new LoginResponse
                {
                    AccessToken = savedToken!,
                    Session = result.Data
                };

                _sessionManager.SetSession(loginResp);
                SetState(AuthenticationState.Authenticated);
                return result;
            }

            SetState(AuthenticationState.Unauthenticated);
            return result;
        }

        private void SetState(AuthenticationState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke(this, newState);
            }
        }

        private async Task<DataResult<LoginResponse>> ExecuteRefreshNetworkCallAsync(string refreshToken, CancellationToken cancellationToken)
        {
            var req = new DataRequest("auth.refresh", new { refreshToken });
            return await _client.ExecuteAsync<LoginResponse>(req, cancellationToken).ConfigureAwait(false);
        }
    }
}
