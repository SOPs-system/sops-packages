using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Domain.Auth
{
    public interface IAuthService
    {
        Task<AuthResultDto> LoginAsync(string username, string password, string? clientIp, CancellationToken ct = default);
        Task<UserInfoDto?> ValidateTokenAsync(string token, CancellationToken ct = default);
        Task<IReadOnlyList<UserInfoDto>> ListUsersAsync(CancellationToken ct = default);
        Task<bool> CreateUserAsync(string username, string displayName, string password, string role, CancellationToken ct = default);
        Task<bool> UpdateUserStatusAsync(string userId, bool isActive, CancellationToken ct = default);
        Task RecordAuditEventAsync(string action, string entityType, string? entityId, string? userId, string? detailsJson, string? clientIp, CancellationToken ct = default);
        Task<IReadOnlyList<AuditEventDto>> GetRecentAuditEventsAsync(int limit = 100, CancellationToken ct = default);
    }

    public class AuthService : IAuthService
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;
        private static readonly ConcurrentDictionary<string, (UserInfoDto User, DateTime ExpiresAt)> _activeSessions = new();

        public AuthService(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<AuthResultDto> LoginAsync(string username, string password, string? clientIp, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResultDto { Success = false, ErrorMessage = "Username and password are required." };
            }

            var hash = DatabaseMigrator.HashPassword(password);

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, username, display_name, role, is_active, created_at
                    FROM local_users
                    WHERE LOWER(username) = LOWER(@u) AND password_hash = @h
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@u", username.Trim());
                cmd.Parameters.AddWithValue("@h", hash);

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var isActive = reader.GetInt32(4) == 1;
                        if (!isActive)
                        {
                            return new AuthResultDto { Success = false, ErrorMessage = "Account is inactive. Contact pharmacy manager." };
                        }

                        var user = new UserInfoDto
                        {
                            Id = reader.GetString(0),
                            Username = reader.GetString(1),
                            DisplayName = reader.GetString(2),
                            Role = reader.GetString(3),
                            IsActive = true,
                            CreatedAt = DateTime.Parse(reader.GetString(5))
                        };

                        // Issue session token valid for 24 hours
                        var token = "sops_tok_" + Guid.NewGuid().ToString("N");
                        _activeSessions[token] = (user, DateTime.UtcNow.AddHours(24));

                        // Audit event
                        await RecordAuditEventAsync("LOGIN", "User", user.Id, user.Id, "{\"status\":\"success\"}", clientIp, ct).ConfigureAwait(false);

                        return new AuthResultDto
                        {
                            Success = true,
                            Token = token,
                            User = user
                        };
                    }
                }
            }

            await RecordAuditEventAsync("LOGIN_FAILED", "User", null, null, $"{{\"attempted_user\":\"{username}\"}}", clientIp, ct).ConfigureAwait(false);
            return new AuthResultDto { Success = false, ErrorMessage = "Invalid credentials." };
        }

        public Task<UserInfoDto?> ValidateTokenAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<UserInfoDto?>(null);

            if (_activeSessions.TryGetValue(token, out var session))
            {
                if (session.ExpiresAt > DateTime.UtcNow)
                {
                    return Task.FromResult<UserInfoDto?>(session.User);
                }
                _activeSessions.TryRemove(token, out _);
            }

            return Task.FromResult<UserInfoDto?>(null);
        }

        public async Task<IReadOnlyList<UserInfoDto>> ListUsersAsync(CancellationToken ct = default)
        {
            var list = new List<UserInfoDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, username, display_name, role, is_active, created_at FROM local_users ORDER BY username ASC;";
                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(new UserInfoDto
                        {
                            Id = reader.GetString(0),
                            Username = reader.GetString(1),
                            DisplayName = reader.GetString(2),
                            Role = reader.GetString(3),
                            IsActive = reader.GetInt32(4) == 1,
                            CreatedAt = DateTime.Parse(reader.GetString(5))
                        });
                    }
                }
            }
            return list;
        }

        public async Task<bool> CreateUserAsync(string username, string displayName, string password, string role, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                var newId = "user_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var hash = DatabaseMigrator.HashPassword(password);
                var now = DateTime.UtcNow.ToString("o");

                cmd.CommandText = @"
                    INSERT INTO local_users (id, username, display_name, password_hash, role, is_active, created_at)
                    VALUES (@id, @u, @name, @h, @r, 1, @now);";
                cmd.Parameters.AddWithValue("@id", newId);
                cmd.Parameters.AddWithValue("@u", username.Trim());
                cmd.Parameters.AddWithValue("@name", displayName.Trim());
                cmd.Parameters.AddWithValue("@h", hash);
                cmd.Parameters.AddWithValue("@r", role);
                cmd.Parameters.AddWithValue("@now", now);

                return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> UpdateUserStatusAsync(string userId, bool isActive, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE local_users SET is_active = @active WHERE id = @id;";
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", userId);
                return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
            }
        }

        public async Task RecordAuditEventAsync(
            string action,
            string entityType,
            string? entityId,
            string? userId,
            string? detailsJson,
            string? clientIp,
            CancellationToken ct = default)
        {
            try
            {
                using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO audit_events (id, timestamp, user_id, action, entity_type, entity_id, details_json, ip_address)
                        VALUES (@id, @ts, @u, @act, @entType, @entId, @dt, @ip);";
                    cmd.Parameters.AddWithValue("@id", "aud_" + Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@u", (object?)userId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@act", action);
                    cmd.Parameters.AddWithValue("@entType", entityType);
                    cmd.Parameters.AddWithValue("@entId", (object?)entityId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@dt", (object?)detailsJson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ip", (object?)clientIp ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
            catch
            {
                // Never crash callers on audit failure
            }
        }

        public async Task<IReadOnlyList<AuditEventDto>> GetRecentAuditEventsAsync(int limit = 100, CancellationToken ct = default)
        {
            var list = new List<AuditEventDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, timestamp, user_id, action, entity_type, entity_id, details_json, ip_address
                    FROM audit_events
                    ORDER BY timestamp DESC
                    LIMIT @lim;";
                cmd.Parameters.AddWithValue("@lim", Math.Max(1, Math.Min(limit, 500)));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(new AuditEventDto
                        {
                            Id = reader.GetString(0),
                            Timestamp = DateTime.Parse(reader.GetString(1)),
                            UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Action = reader.GetString(3),
                            EntityType = reader.GetString(4),
                            EntityId = reader.IsDBNull(5) ? null : reader.GetString(5),
                            DetailsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                            IpAddress = reader.IsDBNull(7) ? null : reader.GetString(7)
                        });
                    }
                }
            }
            return list;
        }
    }
}
