using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Domain.Devices
{
    public interface IDeviceService
    {
        Task<IReadOnlyList<TerminalDto>> GetRegisteredTerminalsAsync(CancellationToken ct = default);
        Task<TerminalDto> EnrollTerminalAsync(string deviceName, string deviceType, string hardwareFingerprint, string? ipAddress, CancellationToken ct = default);
        Task<bool> ApproveTerminalAsync(string terminalId, CancellationToken ct = default);
        Task<bool> RejectTerminalAsync(string terminalId, CancellationToken ct = default);
        Task<bool> RevokeTerminalAsync(string terminalId, CancellationToken ct = default);
        Task<bool> RecordHeartbeatAsync(string terminalId, string? ipAddress, CancellationToken ct = default);
        Task<bool> IsTerminalAuthorizedAsync(string terminalId, CancellationToken ct = default);
    }

    public class DeviceService : IDeviceService
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;

        public DeviceService(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<IReadOnlyList<TerminalDto>> GetRegisteredTerminalsAsync(CancellationToken ct = default)
        {
            var list = new List<TerminalDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, device_name, device_type, hardware_fingerprint, ip_address, status, last_seen_at, created_at
                    FROM registered_terminals
                    ORDER BY created_at DESC;";

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(new TerminalDto
                        {
                            Id = reader.GetString(0),
                            DeviceName = reader.GetString(1),
                            DeviceType = reader.GetString(2),
                            HardwareFingerprint = reader.GetString(3),
                            IpAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Status = reader.GetString(5),
                            LastSeenAt = DateTime.Parse(reader.GetString(6)),
                            CreatedAt = DateTime.Parse(reader.GetString(7))
                        });
                    }
                }
            }
            return list;
        }

        public async Task<TerminalDto> EnrollTerminalAsync(
            string deviceName,
            string deviceType,
            string hardwareFingerprint,
            string? ipAddress,
            CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                // Check if already registered by fingerprint
                cmd.CommandText = @"
                    SELECT id, device_name, device_type, hardware_fingerprint, ip_address, status, last_seen_at, created_at
                    FROM registered_terminals
                    WHERE hardware_fingerprint = @fp
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@fp", hardwareFingerprint);

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var terminal = new TerminalDto
                        {
                            Id = reader.GetString(0),
                            DeviceName = reader.GetString(1),
                            DeviceType = reader.GetString(2),
                            HardwareFingerprint = reader.GetString(3),
                            IpAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Status = reader.GetString(5),
                            LastSeenAt = DateTime.Parse(reader.GetString(6)),
                            CreatedAt = DateTime.Parse(reader.GetString(7))
                        };
                        return terminal;
                    }
                }

                // Enroll new
                var newId = "term_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var now = DateTime.UtcNow.ToString("o");

                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    INSERT INTO registered_terminals (id, device_name, device_type, hardware_fingerprint, ip_address, status, last_seen_at, created_at)
                    VALUES (@id, @name, @type, @fp, @ip, 'Approved', @now, @now);";
                cmd.Parameters.AddWithValue("@id", newId);
                cmd.Parameters.AddWithValue("@name", deviceName);
                cmd.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(deviceType) ? "POS" : deviceType);
                cmd.Parameters.AddWithValue("@fp", hardwareFingerprint);
                cmd.Parameters.AddWithValue("@ip", (object?)ipAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@now", now);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                return new TerminalDto
                {
                    Id = newId,
                    DeviceName = deviceName,
                    DeviceType = deviceType,
                    HardwareFingerprint = hardwareFingerprint,
                    IpAddress = ipAddress,
                    Status = "Approved",
                    LastSeenAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<bool> ApproveTerminalAsync(string terminalId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE registered_terminals SET status = 'Approved' WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", terminalId);
                return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> RejectTerminalAsync(string terminalId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM registered_terminals WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", terminalId);
                return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> RevokeTerminalAsync(string terminalId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE registered_terminals SET status = 'Revoked' WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", terminalId);
                return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> RecordHeartbeatAsync(string terminalId, string? ipAddress, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE registered_terminals 
                    SET last_seen_at = @now, ip_address = COALESCE(@ip, ip_address)
                    WHERE id = @id AND status = 'Approved';";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@ip", (object?)ipAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", terminalId);
                return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> IsTerminalAuthorizedAsync(string terminalId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM registered_terminals WHERE id = @id AND status = 'Approved';";
                cmd.Parameters.AddWithValue("@id", terminalId);
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                return count > 0;
            }
        }
    }
}
