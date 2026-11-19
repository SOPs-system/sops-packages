using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Domain.Shifts
{
    public class ShiftDto
    {
        public string Id { get; set; } = string.Empty;
        public int ShiftNumber { get; set; }
        public string PharmacyId { get; set; } = string.Empty;
        public string TerminalId { get; set; } = string.Empty;
        public string CashierUserId { get; set; } = string.Empty;
        public long OpeningCashMinor { get; set; }
        public long? ClosingCashMinor { get; set; }
        public long? ExpectedCashMinor { get; set; }
        public long? CashDiffMinor { get; set; }
        public string Currency { get; set; } = "EGP";
        public string Status { get; set; } = "Open";
        public string OpenedAt { get; set; } = string.Empty;
        public string? ClosedAt { get; set; }
    }

    public interface IShiftService
    {
        Task<ShiftDto> OpenShiftAsync(string pharmacyId, string terminalId, string cashierUserId, long floatPiastres, CancellationToken ct = default);
        Task<ShiftDto> CloseShiftAsync(string shiftId, long countedPiastres, CancellationToken ct = default);
        Task<long> GetExpectedCashAsync(string shiftId, CancellationToken ct = default);
        Task<ShiftDto?> GetActiveShiftForTerminalAsync(string terminalId, CancellationToken ct = default);
    }

    public class ShiftService : IShiftService
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;

        public ShiftService(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<ShiftDto> OpenShiftAsync(string pharmacyId, string terminalId, string cashierUserId, long floatPiastres, CancellationToken ct = default)
        {
            var active = await GetActiveShiftForTerminalAsync(terminalId, ct).ConfigureAwait(false);
            if (active != null)
            {
                return active; // Reuse existing open shift on terminal
            }

            var shiftId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow.ToString("o");

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(MAX(shift_number), 0) + 1 FROM pos_shifts;";
                var nextNumber = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

                cmd.CommandText = @"
                    INSERT INTO pos_shifts (
                        id, shift_number, pharmacy_id, terminal_id, cashier_user_id,
                        opening_cash_minor, status, opened_at
                    ) VALUES (
                        @id, @snum, @pharma, @term, @cashier,
                        @float, 'Open', @now
                    );";
                cmd.Parameters.AddWithValue("@id", shiftId);
                cmd.Parameters.AddWithValue("@snum", nextNumber);
                cmd.Parameters.AddWithValue("@pharma", pharmacyId);
                cmd.Parameters.AddWithValue("@term", terminalId);
                cmd.Parameters.AddWithValue("@cashier", cashierUserId);
                cmd.Parameters.AddWithValue("@float", floatPiastres);
                cmd.Parameters.AddWithValue("@now", now);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                return new ShiftDto
                {
                    Id = shiftId,
                    ShiftNumber = nextNumber,
                    PharmacyId = pharmacyId,
                    TerminalId = terminalId,
                    CashierUserId = cashierUserId,
                    OpeningCashMinor = floatPiastres,
                    Status = "Open",
                    OpenedAt = now
                };
            }
        }

        public async Task<ShiftDto> CloseShiftAsync(string shiftId, long countedPiastres, CancellationToken ct = default)
        {
            var expected = await GetExpectedCashAsync(shiftId, ct).ConfigureAwait(false);
            var diff = countedPiastres - expected;
            var now = DateTime.UtcNow.ToString("o");

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE pos_shifts
                    SET status = 'Closed',
                        closing_cash_minor = @closing,
                        expected_cash_minor = @expected,
                        cash_diff_minor = @diff,
                        closed_at = @now
                    WHERE id = @id;";
                cmd.Parameters.AddWithValue("@closing", countedPiastres);
                cmd.Parameters.AddWithValue("@expected", expected);
                cmd.Parameters.AddWithValue("@diff", diff);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@id", shiftId);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                // Fetch updated record
                cmd.CommandText = "SELECT * FROM pos_shifts WHERE id = @id LIMIT 1;";
                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return ReadShiftDto(reader);
                    }
                }
            }

            throw new InvalidOperationException($"Shift '{shiftId}' was not found.");
        }

        public async Task<long> GetExpectedCashAsync(string shiftId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT opening_cash_minor FROM pos_shifts WHERE id = @id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", shiftId);
                var opening = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

                cmd.CommandText = @"
                    SELECT COALESCE(SUM(total_minor), 0)
                    FROM pos_transactions
                    WHERE shift_id = @id AND transaction_status = 'Completed' AND transaction_type = 'Sale';";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", shiftId);
                var sales = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

                cmd.CommandText = @"
                    SELECT COALESCE(SUM(total_minor), 0)
                    FROM pos_transactions
                    WHERE shift_id = @id AND transaction_status = 'Refunded';";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", shiftId);
                var refunds = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));

                return opening + sales - refunds;
            }
        }

        public async Task<ShiftDto?> GetActiveShiftForTerminalAsync(string terminalId, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT * FROM pos_shifts
                    WHERE terminal_id = @term AND status = 'Open'
                    ORDER BY opened_at DESC LIMIT 1;";
                cmd.Parameters.AddWithValue("@term", terminalId);

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return ReadShiftDto(reader);
                    }
                }
            }

            return null;
        }

        private static ShiftDto ReadShiftDto(DbDataReader reader)
        {
            return new ShiftDto
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                ShiftNumber = reader.GetInt32(reader.GetOrdinal("shift_number")),
                PharmacyId = reader.GetString(reader.GetOrdinal("pharmacy_id")),
                TerminalId = reader.GetString(reader.GetOrdinal("terminal_id")),
                CashierUserId = reader.GetString(reader.GetOrdinal("cashier_user_id")),
                OpeningCashMinor = reader.GetInt64(reader.GetOrdinal("opening_cash_minor")),
                ClosingCashMinor = reader.IsDBNull(reader.GetOrdinal("closing_cash_minor")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("closing_cash_minor")),
                ExpectedCashMinor = reader.IsDBNull(reader.GetOrdinal("expected_cash_minor")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("expected_cash_minor")),
                CashDiffMinor = reader.IsDBNull(reader.GetOrdinal("cash_diff_minor")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("cash_diff_minor")),
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                OpenedAt = reader.GetString(reader.GetOrdinal("opened_at")),
                ClosedAt = reader.IsDBNull(reader.GetOrdinal("closed_at")) ? null : reader.GetString(reader.GetOrdinal("closed_at"))
            };
        }
    }
}
