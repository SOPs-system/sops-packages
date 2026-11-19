using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Drugs.Normalization;
using Sops.Pharmacy.Server.Domain.Inventory;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Domain.Catalog
{
    public interface IDrugCatalogService
    {
        Task<IReadOnlyList<LocalDrugDto>> SearchDrugsAsync(string? query = null, int limit = 50, CancellationToken ct = default);
        Task<LocalDrugDto?> FindByBarcodeAsync(string barcode, CancellationToken ct = default);
        Task<LocalDrugDto?> FindBySkuAsync(string sku, CancellationToken ct = default);
        Task<LocalDrugDto?> GetByIdAsync(string id, CancellationToken ct = default);
        Task EnsureSampleCatalogSeededAsync(CancellationToken ct = default);
    }

    public class DrugCatalogService : IDrugCatalogService
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;

        public DrugCatalogService(SqlCipherDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task<IReadOnlyList<LocalDrugDto>> SearchDrugsAsync(string? query = null, int limit = 50, CancellationToken ct = default)
        {
            await EnsureSampleCatalogSeededAsync(ct).ConfigureAwait(false);

            var list = new List<LocalDrugDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                var sql = @"
                    SELECT d.id, d.global_drug_id, d.trade_name, d.trade_name_ar, d.generic_name,
                           d.barcode, d.sku, d.dosage_form, d.strength, d.public_price_minor,
                           d.local_cost_minor, d.custom_sell_price_minor, d.units_per_pack, d.currency, d.is_active_locally,
                           COALESCE(SUM(l.remaining_quantity), 0) AS total_stock,
                           COALESCE(SUM(l.reserved_quantity), 0) AS reserved_stock
                    FROM local_drugs d
                    LEFT JOIN stock_lots l ON d.id = l.local_drug_id";

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var cleanQ = query!.Trim();
                    var normQ = cleanQ.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا');
                    sql += @"
                    WHERE d.trade_name LIKE @q 
                       OR d.trade_name_ar LIKE @q 
                       OR REPLACE(REPLACE(REPLACE(d.trade_name_ar, 'أ', 'ا'), 'إ', 'ا'), 'آ', 'ا') LIKE @normQ
                       OR d.generic_name LIKE @q 
                       OR d.barcode LIKE @q 
                       OR d.sku LIKE @q";
                    cmd.Parameters.Add(new SqliteParameter("@q", $"%{cleanQ}%"));
                    cmd.Parameters.Add(new SqliteParameter("@normQ", $"%{normQ}%"));
                }

                sql += " GROUP BY d.id ORDER BY d.trade_name LIMIT " + Math.Max(1, Math.Min(limit, 200)) + ";";
                cmd.CommandText = sql;

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(ReadDrugDto(reader));
                    }
                }
            }

            return list;
        }

        public async Task<LocalDrugDto?> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            await EnsureSampleCatalogSeededAsync(ct).ConfigureAwait(false);

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT d.id, d.global_drug_id, d.trade_name, d.trade_name_ar, d.generic_name,
                           d.barcode, d.sku, d.dosage_form, d.strength, d.public_price_minor,
                           d.local_cost_minor, d.custom_sell_price_minor, d.units_per_pack, d.currency, d.is_active_locally,
                           COALESCE(SUM(l.remaining_quantity), 0) AS total_stock,
                           COALESCE(SUM(l.reserved_quantity), 0) AS reserved_stock
                    FROM local_drugs d
                    LEFT JOIN stock_lots l ON d.id = l.local_drug_id
                    WHERE d.barcode = @b
                    GROUP BY d.id LIMIT 1;";
                cmd.Parameters.Add(new SqliteParameter("@b", barcode.Trim()));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return ReadDrugDto(reader);
                    }
                }
            }

            return null;
        }

        public async Task<LocalDrugDto?> FindBySkuAsync(string sku, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;
            await EnsureSampleCatalogSeededAsync(ct).ConfigureAwait(false);

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT d.id, d.global_drug_id, d.trade_name, d.trade_name_ar, d.generic_name,
                           d.barcode, d.sku, d.dosage_form, d.strength, d.public_price_minor,
                           d.local_cost_minor, d.custom_sell_price_minor, d.units_per_pack, d.currency, d.is_active_locally,
                           COALESCE(SUM(l.remaining_quantity), 0) AS total_stock,
                           COALESCE(SUM(l.reserved_quantity), 0) AS reserved_stock
                    FROM local_drugs d
                    LEFT JOIN stock_lots l ON d.id = l.local_drug_id
                    WHERE d.sku = @s
                    GROUP BY d.id LIMIT 1;";
                cmd.Parameters.Add(new SqliteParameter("@s", sku.Trim().ToUpperInvariant()));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return ReadDrugDto(reader);
                    }
                }
            }

            return null;
        }

        public async Task<LocalDrugDto?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT d.id, d.global_drug_id, d.trade_name, d.trade_name_ar, d.generic_name,
                           d.barcode, d.sku, d.dosage_form, d.strength, d.public_price_minor,
                           d.local_cost_minor, d.custom_sell_price_minor, d.units_per_pack, d.currency, d.is_active_locally,
                           COALESCE(SUM(l.remaining_quantity), 0) AS total_stock,
                           COALESCE(SUM(l.reserved_quantity), 0) AS reserved_stock
                    FROM local_drugs d
                    LEFT JOIN stock_lots l ON d.id = l.local_drug_id
                    WHERE d.id = @id OR d.global_drug_id = @id
                    GROUP BY d.id LIMIT 1;";
                cmd.Parameters.Add(new SqliteParameter("@id", id.Trim()));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return ReadDrugDto(reader);
                    }
                }
            }

            return null;
        }

        public async Task EnsureSampleCatalogSeededAsync(CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM local_drugs;";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
                if (count > 0) return;

                using (var tx = conn.BeginTransaction())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO local_drugs (id, global_drug_id, trade_name, trade_name_ar, generic_name, barcode, sku, dosage_form, strength, public_price_minor, local_cost_minor, custom_sell_price_minor, units_per_pack, currency, created_at) VALUES
                        ('drg_001', 'G-DRG-001', 'Panadol Extra', 'بانادول اكسترا', 'Paracetamol / Caffeine', '622300000101', 'DRG-000001', 'Tablet', '500mg/65mg', 4500, 3600, NULL, 24, 'EGP', @now),
                        ('drg_002', 'G-DRG-002', 'Augmentin 1g', 'أوجمنتين 1 جم', 'Amoxicillin / Clavulanic Acid', '622300000102', 'DRG-000002', 'Tablet', '1000mg', 21000, 16800, NULL, 14, 'EGP', @now),
                        ('drg_003', 'G-DRG-003', 'Brufen Syrup', 'بروفين شراب', 'Ibuprofen', '622300000103', 'DRG-000003', 'Syrup', '100mg/5ml', 3250, 2600, NULL, 1, 'EGP', @now),
                        ('drg_004', 'G-DRG-004', 'Concor 5mg', 'كونكور 5 مجم', 'Bisoprolol Fumarate', '622300000104', 'DRG-000004', 'Tablet', '5mg', 6000, 4800, NULL, 30, 'EGP', @now),
                        ('drg_005', 'G-DRG-005', 'Cataflam 50mg', 'كتافلام 50 مجم', 'Diclofenac Potassium', '622300000105', 'DRG-000005', 'Tablet', '50mg', 5800, 4640, NULL, 20, 'EGP', @now),
                        ('drg_006', 'G-DRG-006', 'Amaryl 2mg', 'أماريل 2 مجم', 'Glimepiride', '622300000106', 'DRG-000006', 'Tablet', '2mg', 4650, 3720, NULL, 30, 'EGP', @now);

                        INSERT INTO stock_lots (id, local_drug_id, global_drug_id, batch_number, expiry_date, received_quantity, remaining_quantity, reserved_quantity, base_unit, purchase_price_minor, currency, received_date) VALUES
                        ('lot_001_1', 'drg_001', 'G-DRG-001', 'B2026-A1', '2027-06-30', 100, 85, 0, 'Box', 3600, 'EGP', @now),
                        ('lot_001_2', 'drg_001', 'G-DRG-001', 'B2026-A2', '2028-01-31', 50, 50, 0, 'Box', 3600, 'EGP', @now),
                        ('lot_002_1', 'drg_002', 'G-DRG-002', 'AUG-991', '2026-12-31', 40, 28, 0, 'Box', 16800, 'EGP', @now),
                        ('lot_003_1', 'drg_003', 'G-DRG-003', 'BRF-404', '2027-03-31', 60, 58, 0, 'Bottle', 2600, 'EGP', @now),
                        ('lot_004_1', 'drg_004', 'G-DRG-004', 'CNC-552', '2027-09-30', 50, 45, 0, 'Box', 4800, 'EGP', @now),
                        ('lot_005_1', 'drg_005', 'G-DRG-005', 'CAT-881', '2026-11-30', 30, 18, 0, 'Box', 4640, 'EGP', @now),
                        ('lot_006_1', 'drg_006', 'G-DRG-006', 'AMR-112', '2028-04-30', 50, 50, 0, 'Box', 3720, 'EGP', @now);";
                    cmd.Parameters.Add(new SqliteParameter("@now", DateTime.UtcNow.ToString("o")));
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    tx.Commit();
                }
            }
        }

        private static LocalDrugDto ReadDrugDto(DbDataReader reader)
        {
            return new LocalDrugDto
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                GlobalDrugId = reader.GetString(reader.GetOrdinal("global_drug_id")),
                TradeName = reader.GetString(reader.GetOrdinal("trade_name")),
                TradeNameAr = reader.GetString(reader.GetOrdinal("trade_name_ar")),
                GenericName = reader.GetString(reader.GetOrdinal("generic_name")),
                Barcode = reader.GetString(reader.GetOrdinal("barcode")),
                Sku = reader.GetString(reader.GetOrdinal("sku")),
                DosageForm = reader.GetString(reader.GetOrdinal("dosage_form")),
                Strength = reader.GetString(reader.GetOrdinal("strength")),
                PublicPriceMinor = reader.GetInt64(reader.GetOrdinal("public_price_minor")),
                LocalCostMinor = reader.GetInt64(reader.GetOrdinal("local_cost_minor")),
                CustomSellPriceMinor = reader.IsDBNull(reader.GetOrdinal("custom_sell_price_minor")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("custom_sell_price_minor")),
                UnitsPerPack = reader.GetInt32(reader.GetOrdinal("units_per_pack")),
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                IsActiveLocally = reader.GetInt32(reader.GetOrdinal("is_active_locally")) == 1,
                TotalStockQuantity = reader.GetDecimal(reader.GetOrdinal("total_stock")),
                ReservedQuantity = reader.GetDecimal(reader.GetOrdinal("reserved_stock"))
            };
        }
    }
}
