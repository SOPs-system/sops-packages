using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Domain.Auth;
using Sops.Pharmacy.Server.Domain.Devices;
using Sops.Pharmacy.Server.Hosting;

namespace Sops.Pharmacy.Server.Management
{
    public class ServerStatusSummary
    {
        public bool IsRunning { get; set; }
        public string ServerName { get; set; } = "SOPs Pharmacy Local Server";
        public string Version { get; set; } = "1.0.0";
        public string Uptime { get; set; } = "00:00:00";
        public double UptimeTotalSeconds { get; set; }
        public int ActiveTerminalsCount { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public string DatabasePath { get; set; } = string.Empty;
        public long MemoryUsageMb { get; set; }
        public int Port { get; set; } = 5040;
        public int PendingOutbox { get; set; }
        public int PendingInbox { get; set; }
    }

    public class BackupItemDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ManagementClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly PharmacyServerSettings _settings;
        private ServerHost? _embeddedHost;
        private bool _disposed;

        public int Port => _settings.Port;
        public bool IsEmbeddedHostRunning => _embeddedHost != null && _embeddedHost.IsRunning;

        public ManagementClient(PharmacyServerSettings? settings = null)
        {
            _settings = settings ?? ServerConfigLoader.Load();
            _http = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{_settings.Port}/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public async Task<bool> IsServerReachableAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync("health", ct).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task EnsureServerRunningAsync(CancellationToken ct = default)
        {
            if (await IsServerReachableAsync(ct).ConfigureAwait(false))
            {
                return;
            }

            // Start embedded host for local management console
            if (_embeddedHost == null)
            {
                _embeddedHost = new ServerHost(_settings);
                await _embeddedHost.StartAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task<ServerStatusSummary> GetStatusAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync("rpc/management/status", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");

                    return new ServerStatusSummary
                    {
                        IsRunning = true,
                        ServerName = data.GetProperty("serverName").GetString() ?? "SOPs Pharmacy Local Server",
                        Version = data.GetProperty("version").GetString() ?? "1.0.0",
                        Uptime = data.GetProperty("uptime").GetString() ?? "00:00:00",
                        UptimeTotalSeconds = data.GetProperty("uptimeTotalSeconds").GetDouble(),
                        ActiveTerminalsCount = data.GetProperty("activeTerminalsCount").GetInt32(),
                        DatabaseSizeBytes = data.GetProperty("databaseSizeBytes").GetInt64(),
                        DatabasePath = data.GetProperty("databasePath").GetString() ?? "",
                        MemoryUsageMb = data.GetProperty("memoryUsageMb").GetInt64(),
                        Port = data.GetProperty("port").GetInt32(),
                        PendingOutbox = data.GetProperty("pendingOutbox").GetInt32(),
                        PendingInbox = data.GetProperty("pendingInbox").GetInt32()
                    };
                }
            }
            catch
            {
                // Fallback when unreachable
            }

            var dbFile = new FileInfo(_settings.DatabasePath);
            return new ServerStatusSummary
            {
                IsRunning = false,
                DatabasePath = _settings.DatabasePath,
                DatabaseSizeBytes = dbFile.Exists ? dbFile.Length : 0,
                Port = _settings.Port
            };
        }

        public async Task<IReadOnlyList<TerminalDto>> GetTerminalsAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync("rpc/management/terminals", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");

                    var list = new List<TerminalDto>();
                    foreach (var item in data.EnumerateArray())
                    {
                        list.Add(new TerminalDto
                        {
                            Id = item.GetProperty("id").GetString() ?? "",
                            DeviceName = item.GetProperty("deviceName").GetString() ?? "",
                            DeviceType = item.GetProperty("deviceType").GetString() ?? "POS",
                            HardwareFingerprint = item.GetProperty("hardwareFingerprint").GetString() ?? "",
                            IpAddress = item.TryGetProperty("ipAddress", out var ip) && !ip.ValueKind.Equals(JsonValueKind.Null) ? ip.GetString() : null,
                            Status = item.GetProperty("status").GetString() ?? "Pending",
                            LastSeenAt = DateTime.Parse(item.GetProperty("lastSeenAt").GetString() ?? DateTime.UtcNow.ToString("o")),
                            CreatedAt = DateTime.Parse(item.GetProperty("createdAt").GetString() ?? DateTime.UtcNow.ToString("o"))
                        });
                    }
                    return list;
                }
            }
            catch { }
            return Array.Empty<TerminalDto>();
        }

        public async Task<bool> ApproveTerminalAsync(string terminalId, CancellationToken ct = default)
        {
            var content = new StringContent(JsonSerializer.Serialize(new { id = terminalId }), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("rpc/management/terminals/approve", content, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> RevokeTerminalAsync(string terminalId, CancellationToken ct = default)
        {
            var content = new StringContent(JsonSerializer.Serialize(new { id = terminalId }), Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("rpc/management/terminals/revoke", content, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }

        public async Task<IReadOnlyList<AuditEventDto>> GetAuditLogsAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync("rpc/management/logs", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");

                    var list = new List<AuditEventDto>();
                    foreach (var item in data.EnumerateArray())
                    {
                        list.Add(new AuditEventDto
                        {
                            Id = item.GetProperty("id").GetString() ?? "",
                            Timestamp = DateTime.Parse(item.GetProperty("timestamp").GetString() ?? DateTime.UtcNow.ToString("o")),
                            UserId = item.TryGetProperty("userId", out var u) && !u.ValueKind.Equals(JsonValueKind.Null) ? u.GetString() : null,
                            Action = item.GetProperty("action").GetString() ?? "",
                            EntityType = item.GetProperty("entityType").GetString() ?? "",
                            EntityId = item.TryGetProperty("entityId", out var eid) && !eid.ValueKind.Equals(JsonValueKind.Null) ? eid.GetString() : null,
                            DetailsJson = item.TryGetProperty("detailsJson", out var dj) && !dj.ValueKind.Equals(JsonValueKind.Null) ? dj.GetString() : null,
                            IpAddress = item.TryGetProperty("ipAddress", out var ip) && !ip.ValueKind.Equals(JsonValueKind.Null) ? ip.GetString() : null
                        });
                    }
                    return list;
                }
            }
            catch { }
            return Array.Empty<AuditEventDto>();
        }

        public async Task<IReadOnlyList<UserInfoDto>> GetUsersAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync("rpc/management/users", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");

                    var list = new List<UserInfoDto>();
                    foreach (var item in data.EnumerateArray())
                    {
                        list.Add(new UserInfoDto
                        {
                            Id = item.GetProperty("id").GetString() ?? "",
                            Username = item.GetProperty("username").GetString() ?? "",
                            DisplayName = item.GetProperty("displayName").GetString() ?? "",
                            Role = item.GetProperty("role").GetString() ?? "Cashier",
                            IsActive = item.GetProperty("isActive").GetBoolean(),
                            CreatedAt = DateTime.Parse(item.GetProperty("createdAt").GetString() ?? DateTime.UtcNow.ToString("o"))
                        });
                    }
                    return list;
                }
            }
            catch { }
            return Array.Empty<UserInfoDto>();
        }

        public async Task<IReadOnlyList<BackupItemDto>> GetBackupHistoryAsync(CancellationToken ct = default)
        {
            try
            {
                var resp = await _http.GetAsync("rpc/management/backup/history", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var data = doc.RootElement.GetProperty("data");

                    var list = new List<BackupItemDto>();
                    foreach (var item in data.EnumerateArray())
                    {
                        list.Add(new BackupItemDto
                        {
                            FileName = item.GetProperty("fileName").GetString() ?? "",
                            FullPath = item.GetProperty("fullPath").GetString() ?? "",
                            SizeBytes = item.GetProperty("sizeBytes").GetInt64(),
                            CreatedAt = DateTime.Parse(item.GetProperty("createdAt").GetString() ?? DateTime.UtcNow.ToString("o"))
                        });
                    }
                    return list;
                }
            }
            catch { }
            return Array.Empty<BackupItemDto>();
        }

        public async Task<bool> TriggerBackupAsync(CancellationToken ct = default)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(_settings.BackupDirectory, $"sops_manual_backup_{timestamp}.bak");

            try
            {
                if (File.Exists(_settings.DatabasePath))
                {
                    using (var sourceStream = new FileStream(_settings.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(backupFile, FileMode.Create, FileAccess.Write))
                    {
                        await sourceStream.CopyToAsync(destStream).ConfigureAwait(false);
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http.Dispose();
            // Note: closing the management client does NOT stop the Windows service!
            // If we started an embedded host, we can stop it if in standalone management mode.
            _embeddedHost?.Stop();
            _embeddedHost?.Dispose();
        }
    }
}
