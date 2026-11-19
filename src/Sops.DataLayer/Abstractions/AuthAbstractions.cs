using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Abstractions
{
    /// <summary>
    /// Coordinates authentication lifecycle, login, token refresh, and session state.
    /// </summary>
    public interface IAuthenticationService
    {
        AuthenticationState State { get; }
        SessionInfo? CurrentSession { get; }
        string? CurrentAccessToken { get; }
        string? CloudAccessToken { get; }

        event EventHandler<AuthenticationState>? StateChanged;

        Task<DataResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
        Task<DataResult> LogoutAsync(CancellationToken cancellationToken = default);
        Task<DataResult<string>> RefreshTokenAsync(CancellationToken cancellationToken = default);
        Task<DataResult<SessionInfo>> RestoreSessionAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tracks active user session, token expiry, roles, and permissions.
    /// Strictly separates local pharmacy POS credentials from global cloud backend credentials.
    /// </summary>
    public interface ISessionManager
    {
        SessionInfo? CurrentSession { get; }
        string? CurrentAccessToken { get; }
        string? LocalAccessToken { get; }
        string? CloudAccessToken { get; }
        bool IsAuthenticated { get; }
        bool IsExpired { get; }
        IReadOnlyList<string> Permissions { get; }
        IReadOnlyList<string> Roles { get; }

        void SetSession(LoginResponse response);
        void SetCloudSession(string accessToken, string? refreshToken = null, DateTimeOffset? expiresAt = null);
        void UpdateAccessToken(string accessToken, DateTimeOffset expiresAtUtc);
        void ClearSession();
        void ClearCloudSession();
    }

    /// <summary>
    /// Platform-secure token and credential persistence (DPAPI / Windows Credential Manager).
    /// </summary>
    public interface ISecureCredentialStore
    {
        Task SaveTokenAsync(string key, string token, CancellationToken cancellationToken = default);
        Task<string?> GetTokenAsync(string key, CancellationToken cancellationToken = default);
        Task DeleteTokenAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides client-side permission querying and caching.
    /// Note: Server authorization remains authoritative.
    /// </summary>
    public interface IAuthorizationStateProvider
    {
        bool HasPermission(string permission);
        bool IsInRole(string role);
        IReadOnlyList<string> GetPermissions();
        Task RefreshPermissionsAsync(CancellationToken cancellationToken = default);
    }
}
