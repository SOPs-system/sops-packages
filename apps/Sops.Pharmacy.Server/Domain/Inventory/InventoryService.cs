using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Domain.Inventory
{
    public interface IInventoryService
    {
        Task<IReadOnlyList<StockLotDto>> GetStockSummaryAsync(CancellationToken ct = default);
        Task<IReadOnlyList<StockLotDto>> GetLotsForDrugAsync(string drugId, CancellationToken ct = default);
        Task<StockLotDto?> AdjustLotAsync(string lotId, decimal newQuantity, string reason, string? cashierId = null, CancellationToken ct = default);
        Task<IReadOnlyList<StockMovementDto>> GetRecentMovementsAsync(int limit = 50, CancellationToken ct = default);
    }

    public class InventoryService : IInventoryService
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;

        public InventoryService(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<IReadOnlyList<StockLotDto>> GetStockSummaryAsync(CancellationToken ct = default)
        {
            var list = new List<StockLotDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, local_drug_id, global_drug_id, batch_number, expiry_date,
                           received_quantity, remaining_quantity, reserved_quantity,
                           base_unit, purchase_price_minor, currency, distributor_id, received_date
                    FROM stock_lots
                    ORDER BY expiry_date ASC
                    LIMIT 200;";

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(ReadLotDto(reader));
                    }
                }
            }
            return list;
        }

        public async Task<IReadOnlyList<StockLotDto>> GetLotsForDrugAsync(string drugId, CancellationToken ct = default)
        {
            var list = new List<StockLotDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, local_drug_id, global_drug_id, batch_number, expiry_date,
                           received_quantity, remaining_quantity, reserved_quantity,
                           base_unit, purchase_price_minor, currency, distributor_id, received_date
                    FROM stock_lots
                    WHERE local_drug_id = @id OR global_drug_id = @id
                    ORDER BY expiry_date ASC;";
                cmd.Parameters.Add(new SqliteParameter("@id", drugId.Trim()));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(ReadLotDto(reader));
                    }
                }
            }

            return list;
        }

        public async Task<StockLotDto?> AdjustLotAsync(string lotId, decimal newQuantity, string reason, string? cashierId = null, CancellationToken ct = default)
        {
            if (newQuantity < 0) throw new ArgumentException("Quantity cannot be negative", nameof(newQuantity));

            using (var conn = (SqliteConnection)await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var tx = conn.BeginTransaction())
            {
                StockLotDto? lot = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        SELECT id, local_drug_id, global_drug_id, batch_number, expiry_date,
                               received_quantity, remaining_quantity, reserved_quantity,
                               base_unit, purchase_price_minor, currency, distributor_id, received_date
                        FROM stock_lots WHERE id = @id LIMIT 1;";
                    cmd.Parameters.Add(new SqliteParameter("@id", lotId.Trim()));

                    using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(ct).ConfigureAwait(false))
                        {
                            lot = ReadLotDto(reader);
                        }
                    }

                    if (lot == null) return null;

                    var oldQty = lot.RemainingQuantity;
                    var now = DateTime.UtcNow.ToString("o");

                    // Update lot
                    cmd.CommandText = @"
                        UPDATE stock_lots
                        SET remaining_quantity = @newQty
                        WHERE id = @id;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(new SqliteParameter("@newQty", newQuantity));
                    cmd.Parameters.Add(new SqliteParameter("@id", lotId));
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    // Insert movement
                    var movementId = Guid.NewGuid().ToString("N");
                    cmd.CommandText = @"
                        INSERT INTO stock_movements (id, lot_id, drug_id, movement_type, quantity, previous_quantity, new_quantity, reference_id, notes, created_at)
                        VALUES (@mid, @lotId, @drugId, 'Adjustment', @diff, @oldQty, @newQty, @refId, @notes, @now);";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(new SqliteParameter("@mid", movementId));
                    cmd.Parameters.Add(new SqliteParameter("@lotId", lot.Id));
                    cmd.Parameters.Add(new SqliteParameter("@drugId", lot.LocalDrugId));
                    cmd.Parameters.Add(new SqliteParameter("@diff", newQuantity - oldQty));
                    cmd.Parameters.Add(new SqliteParameter("@oldQty", oldQty));
                    cmd.Parameters.Add(new SqliteParameter("@newQty", newQuantity));
                    cmd.Parameters.Add(new SqliteParameter("@refId", cashierId ?? "admin"));
                    cmd.Parameters.Add(new SqliteParameter("@notes", reason ?? "Manual inventory adjustment"));
                    cmd.Parameters.Add(new SqliteParameter("@now", now));
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    // Record Outbox event for synchronization
                    var eventId = Guid.NewGuid().ToString("N");
                    var payload = "{\"lotId\":\"" + lot.Id + "\",\"newQuantity\":" + newQuantity + ",\"reason\":\"" + (reason ?? "") + "\"}";
                    cmd.CommandText = @"
                        INSERT INTO outbox_events (id, aggregate_type, aggregate_id, event_type, payload_json, created_at, status)
                        VALUES (@eid, 'StockLot', @aggId, 'StockAdjusted', @payload, @now, 'Pending');";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(new SqliteParameter("@eid", eventId));
                    cmd.Parameters.Add(new SqliteParameter("@aggId", lot.Id));
                    cmd.Parameters.Add(new SqliteParameter("@payload", payload));
                    cmd.Parameters.Add(new SqliteParameter("@now", now));
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    lot.RemainingQuantity = newQuantity;
                }

                tx.Commit();
                return lot;
            }
        }

        public async Task<IReadOnlyList<StockMovementDto>> GetRecentMovementsAsync(int limit = 50, CancellationToken ct = default)
        {
            var list = new List<StockMovementDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, lot_id, drug_id, movement_type, quantity, previous_quantity, new_quantity, reference_id, notes, created_at
                    FROM stock_movements
                    ORDER BY created_at DESC
                    LIMIT " + Math.Max(1, Math.Min(limit, 100)) + ";";

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(new StockMovementDto
                        {
                            Id = reader.GetString(reader.GetOrdinal("id")),
                            LotId = reader.GetString(reader.GetOrdinal("lot_id")),
                            DrugId = reader.GetString(reader.GetOrdinal("drug_id")),
                            MovementType = reader.GetString(reader.GetOrdinal("movement_type")),
                            Quantity = reader.GetDecimal(reader.GetOrdinal("quantity")),
                            PreviousQuantity = reader.GetDecimal(reader.GetOrdinal("previous_quantity")),
                            NewQuantity = reader.GetDecimal(reader.GetOrdinal("new_quantity")),
                            ReferenceId = reader.IsDBNull(reader.GetOrdinal("reference_id")) ? null : reader.GetString(reader.GetOrdinal("reference_id")),
                            Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")))
                        });
                    }
                }
            }

            return list;
        }

        private static StockLotDto ReadLotDto(DbDataReader reader)
        {
            return new StockLotDto
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                LocalDrugId = reader.GetString(reader.GetOrdinal("local_drug_id")),
                GlobalDrugId = reader.GetString(reader.GetOrdinal("global_drug_id")),
                BatchNumber = reader.GetString(reader.GetOrdinal("batch_number")),
                ExpiryDate = reader.GetString(reader.GetOrdinal("expiry_date")),
                ReceivedQuantity = reader.GetDecimal(reader.GetOrdinal("received_quantity")),
                RemainingQuantity = reader.GetDecimal(reader.GetOrdinal("remaining_quantity")),
                ReservedQuantity = reader.GetDecimal(reader.GetOrdinal("reserved_quantity")),
                BaseUnit = reader.GetString(reader.GetOrdinal("base_unit")),
                PurchasePriceMinor = reader.GetInt64(reader.GetOrdinal("purchase_price_minor")),
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                DistributorId = reader.IsDBNull(reader.GetOrdinal("distributor_id")) ? null : reader.GetString(reader.GetOrdinal("distributor_id")),
                ReceivedDate = reader.GetString(reader.GetOrdinal("received_date"))
            };
        }
    }
}
