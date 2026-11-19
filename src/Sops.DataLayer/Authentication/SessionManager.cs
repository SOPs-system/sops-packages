using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Models.Authentication;

namespace Sops.DataLayer.Authentication
{
    /// <summary>
    /// Thread-safe manager for user session lifecycle in the desktop POS.
    /// Strictly separates local pharmacy POS credentials from global cloud backend credentials.
    /// </summary>
    public sealed class SessionManager : ISessionManager, IDisposable
    {
        private readonly ISecureCredentialStore _credentialStore;
        private readonly ILogger<SessionManager>? _logger;
        private readonly object _syncLock = new object();
        private SessionInfo? _currentSession;
        private IReadOnlyList<string> _permissions = Array.Empty<string>();
        private IReadOnlyList<string> _roles = Array.Empty<string>();
        private string? _currentAccessToken;
        private string? _cloudAccessToken;
        private DateTimeOffset? _cloudExpiresAtUtc;
        private Timer? _expiryTimer;

        public event Action<SessionInfo?>? SessionChanged;
        public event Action? SessionExpired;

        public SessionInfo? CurrentSession
        {
            get
            {
                lock (_syncLock) return _currentSession;
            }
        }

        public string? CurrentAccessToken
        {
            get
            {
                lock (_syncLock) return _currentAccessToken;
            }
        }

        public string? LocalAccessToken => CurrentAccessToken;

        public string? CloudAccessToken
        {
            get
            {
                lock (_syncLock) return _cloudAccessToken;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                lock (_syncLock) return _currentSession != null && !_currentSession.IsExpired;
            }
        }

        public bool IsExpired
        {
            get
            {
                lock (_syncLock) return _currentSession == null || _currentSession.IsExpired;
            }
        }

        public IReadOnlyList<string> Permissions
        {
            get
            {
                lock (_syncLock) return _permissions;
            }
        }

        public IReadOnlyList<string> Roles
        {
            get
            {
                lock (_syncLock) return _roles;
            }
        }

        public SessionManager(ISecureCredentialStore credentialStore, ILogger<SessionManager>? logger = null)
        {
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _logger = logger;
        }

        public void SetSession(LoginResponse response)
        {
            if (response == null)
            {
                ClearSession();
                return;
            }

            Action<SessionInfo?>? handler = null;
            lock (_syncLock)
            {
                _currentSession = response.Session;
                _permissions = response.Permissions ?? Array.Empty<string>();
                _roles = response.Roles ?? Array.Empty<string>();
                _currentAccessToken = response.AccessToken;
                handler = SessionChanged;
                ScheduleExpiryCheck(response.Session);
            }

            if (!string.IsNullOrEmpty(response.AccessToken))
            {
                _credentialStore.SaveTokenAsync("current_session_token", response.AccessToken).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    _credentialStore.SaveTokenAsync("current_refresh_token", response.RefreshToken!).GetAwaiter().GetResult();
                }
            }

            handler?.Invoke(response.Session);
        }

        public void SetCloudSession(string accessToken, string? refreshToken = null, DateTimeOffset? expiresAt = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                ClearCloudSession();
                return;
            }

            lock (_syncLock)
            {
                _cloudAccessToken = accessToken;
                _cloudExpiresAtUtc = expiresAt ?? DateTimeOffset.UtcNow.AddHours(24);
            }

            _credentialStore.SaveTokenAsync("cloud_access_token", accessToken).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                _credentialStore.SaveTokenAsync("cloud_refresh_token", refreshToken!).GetAwaiter().GetResult();
            }
        }

        public void UpdateAccessToken(string accessToken, DateTimeOffset expiresAtUtc)
        {
            Action<SessionInfo?>? handler = null;
            lock (_syncLock)
            {
                if (_currentSession == null) return;

                _currentAccessToken = accessToken;
                _currentSession.ExpiresAtUtc = expiresAtUtc;

                handler = SessionChanged;
                ScheduleExpiryCheck(_currentSession);
            }

            _credentialStore.SaveTokenAsync("current_session_token", accessToken).GetAwaiter().GetResult();
            handler?.Invoke(_currentSession);
        }

        public void ClearSession()
        {
            Action<SessionInfo?>? changedHandler = null;
            lock (_syncLock)
            {
                _currentSession = null;
                _permissions = Array.Empty<string>();
                _roles = Array.Empty<string>();
                _currentAccessToken = null;
                _expiryTimer?.Dispose();
                _expiryTimer = null;
                changedHandler = SessionChanged;
            }

            try
            {
                _credentialStore.DeleteTokenAsync("current_session_token").GetAwaiter().GetResult();
                _credentialStore.DeleteTokenAsync("current_refresh_token").GetAwaiter().GetResult();
            }
            catch { }

            changedHandler?.Invoke(null);
        }

        public void ClearCloudSession()
        {
            lock (_syncLock)
            {
                _cloudAccessToken = null;
                _cloudExpiresAtUtc = null;
            }

            try
            {
                _credentialStore.DeleteTokenAsync("cloud_access_token").GetAwaiter().GetResult();
                _credentialStore.DeleteTokenAsync("cloud_refresh_token").GetAwaiter().GetResult();
            }
            catch { }
        }

        private void ScheduleExpiryCheck(SessionInfo? session)
        {
            _expiryTimer?.Dispose();
            _expiryTimer = null;

            if (session == null) return;

            var timeUntilExpiry = session.ExpiresAtUtc - DateTimeOffset.UtcNow;
            if (timeUntilExpiry <= TimeSpan.Zero)
            {
                OnExpired();
                return;
            }

            _expiryTimer = new Timer(_ => OnExpired(), null, timeUntilExpiry, Timeout.InfiniteTimeSpan);
        }

        private void OnExpired()
        {
            Action? expiredHandler = null;
            lock (_syncLock)
            {
                if (_currentSession != null && _currentSession.IsExpired)
                {
                    _logger?.LogInformation("User session {SessionId} expired", _currentSession.SessionId);
                    expiredHandler = SessionExpired;
                }
            }
            expiredHandler?.Invoke();
        }

        public void Dispose()
        {
            _expiryTimer?.Dispose();
        }
    }
}
