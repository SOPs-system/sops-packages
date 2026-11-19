using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Authentication
{
    /// <summary>
    /// Coordinates single-flight token refresh to prevent thundering herd / refresh stampedes.
    /// </summary>
    public sealed class TokenRefreshCoordinator
    {
        private readonly ISessionManager _sessionManager;
        private readonly ISecureCredentialStore _credentialStore;
        private readonly Func<string, CancellationToken, Task<DataResult<LoginResponse>>> _refreshAction;
        private readonly ILogger<TokenRefreshCoordinator>? _logger;

        private readonly object _lock = new object();
        private Task<DataResult<LoginResponse>>? _currentRefreshTask;

        public TokenRefreshCoordinator(
            ISessionManager sessionManager,
            ISecureCredentialStore credentialStore,
            Func<string, CancellationToken, Task<DataResult<LoginResponse>>> refreshAction,
            ILogger<TokenRefreshCoordinator>? logger = null)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _refreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
            _logger = logger;
        }

        public async Task<DataResult<LoginResponse>> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            Task<DataResult<LoginResponse>> taskToAwait;

            lock (_lock)
            {
                if (_currentRefreshTask != null && !_currentRefreshTask.IsCompleted)
                {
                    _logger?.LogDebug("Awaiting in-flight token refresh");
                    taskToAwait = _currentRefreshTask;
                }
                else
                {
                    _logger?.LogInformation("Initiating single-flight token refresh");
                    taskToAwait = _currentRefreshTask = ExecuteRefreshAsync();
                }
            }

            return await taskToAwait.ConfigureAwait(false);
        }

        private async Task<DataResult<LoginResponse>> ExecuteRefreshAsync()
        {
            try
            {
                string? refreshToken = await _credentialStore.GetTokenAsync("current_refresh_token").ConfigureAwait(false);

                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger?.LogWarning("Cannot refresh token: no refresh token available in secure credential store");
                    return DataResult<LoginResponse>.Unauthorized("No refresh token available to renew session.");
                }

                var result = await _refreshAction(refreshToken!, CancellationToken.None).ConfigureAwait(false);

                if (result.IsSuccess && result.Data != null)
                {
                    _sessionManager.UpdateAccessToken(result.Data.AccessToken, result.Data.ExpiresAtUtc);
                    if (!string.IsNullOrEmpty(result.Data.RefreshToken))
                    {
                        await _credentialStore.SaveTokenAsync("current_refresh_token", result.Data.RefreshToken!).ConfigureAwait(false);
                    }
                    _logger?.LogInformation("Session successfully renewed for user {Username}", result.Data.Session?.Username);
                }
                else
                {
                    _logger?.LogWarning("Token refresh failed: {ErrorCode} - {ErrorMessage}", result.Error?.Code, result.Error?.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during token refresh");
                return DataResult<LoginResponse>.Failure(
                    DataOutcome.Failure,
                    DataError.Create("REFRESH_EXCEPTION", ex.Message));
            }
            finally
            {
                lock (_lock)
                {
                    _currentRefreshTask = null;
                }
            }
        }
    }
}
