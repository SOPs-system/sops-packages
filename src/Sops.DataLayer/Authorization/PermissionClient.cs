using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Endpoints;

namespace Sops.DataLayer.Authorization
{
    /// <summary>
    /// Thread-safe client for evaluating user roles, permissions, and endpoint access rights.
    /// Note: Server authorization remains authoritative.
    /// </summary>
    public sealed class PermissionClient : IAuthorizationStateProvider
    {
        private readonly ISessionManager _sessionManager;

        public PermissionClient(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public bool HasPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission)) return true;
            if (!_sessionManager.IsAuthenticated) return false;

            var perms = _sessionManager.Permissions;
            if (perms == null || perms.Count == 0) return false;

            return perms.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase) || string.Equals(p, "*", StringComparison.OrdinalIgnoreCase));
        }

        public bool IsInRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return true;
            if (!_sessionManager.IsAuthenticated) return false;

            var roles = _sessionManager.Roles;
            if (roles != null && roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var perms = _sessionManager.Permissions;
            if (perms != null && perms.Any(p => string.Equals(p, role, StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(p, $"role:{role}", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        public IReadOnlyList<string> GetPermissions()
        {
            if (!_sessionManager.IsAuthenticated) return Array.Empty<string>();
            return _sessionManager.Permissions ?? Array.Empty<string>();
        }

        public Task RefreshPermissionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public bool CanAccess(EndpointDefinition endpoint)
        {
            if (endpoint == null) return false;
            if (!endpoint.RequiresAuthentication) return true;
            if (!_sessionManager.IsAuthenticated) return false;

            if (!string.IsNullOrWhiteSpace(endpoint.RequiredPermission))
            {
                if (!HasPermission(endpoint.RequiredPermission!)) return false;
            }

            return true;
        }
    }
}
