using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Domain.Catalog;
using Sops.Pharmacy.Server.Domain.Inventory;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Domain.Sales
{
    public interface ISalesService
    {
        Task<SaleDraft> CreateDraftAsync(string? patientId = null, string? reason = null);
        Task<SaleDraft> AddLineAsync(string saleId, string drugId, decimal quantity, int unitRatio = 1, CancellationToken ct = default);
        Task<SaleDraft> AddPaymentAsync(string saleId, string method, long tenderedPiastres);
        Task<TransactionDto> CompleteTransactionAsync(string saleId, string pharmacyId, string terminalId, string cashierId, string shiftId, string? idempotencyKey = null, CancellationToken ct = default);
        Task<TransactionDto?> GetByIdAsync(string id, CancellationToken ct = default);
        Task<IReadOnlyList<TransactionDto>> ListRecentTransactionsAsync(int offset = 0, int limit = 50, CancellationToken ct = default);
        Task<TransactionDto> RefundTransactionAsync(string transactionId, string reason, string cashierId, CancellationToken ct = default);
    }

    public class SalesService : ISalesService
    {
        private readonly SqlCipherDatabaseProvider _dbProvider;
        private readonly IDrugCatalogService _catalogService;
        private readonly ConcurrentDictionary<string, SaleDraft> _drafts = new ConcurrentDictionary<string, SaleDraft>();

        public SalesService(SqlCipherDatabaseProvider dbProvider, IDrugCatalogService catalogService)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        }

        public Task<SaleDraft> CreateDraftAsync(string? patientId = null, string? reason = null)
        {
            var draft = new SaleDraft
            {
                PatientId = patientId,
                Reason = reason
            };
            _drafts[draft.SaleId] = draft;
            return Task.FromResult(draft);
        }

        public async Task<SaleDraft> AddLineAsync(string saleId, string drugId, decimal quantity, int unitRatio = 1, CancellationToken ct = default)
        {
            if (!_drafts.TryGetValue(saleId, out var draft))
            {
                draft = new SaleDraft { SaleId = saleId };
                _drafts[saleId] = draft;
            }

            var drug = await _catalogService.GetByIdAsync(drugId, ct).ConfigureAwait(false);
            if (drug == null)
            {
                throw new InvalidOperationException($"Drug with ID '{drugId}' was not found in catalog.");
            }

            var line = new SaleLineDraft
            {
                DrugId = drug.Id,
                DrugName = drug.TradeName,
                Quantity = quantity,
                UnitRatio = unitRatio > 0 ? unitRatio : 1,
                UnitPriceMinor = drug.PublicPriceMinor,
                PackageTier = "Unit"
            };

            draft.Lines.Add(line);
            return draft;
        }

        public Task<SaleDraft> AddPaymentAsync(string saleId, string method, long tenderedPiastres)
        {
            if (!_drafts.TryGetValue(saleId, out var draft))
            {
                draft = new SaleDraft { SaleId = saleId };
                _drafts[saleId] = draft;
            }

            draft.Payments.Add(new PaymentDraft
            {
                Method = method,
                TenderedPiastres = tenderedPiastres
            });

            return Task.FromResult(draft);
        }

        public async Task<TransactionDto> CompleteTransactionAsync(
            string saleId,
            string pharmacyId,
            string terminalId,
            string cashierId,
            string shiftId,
            string? idempotencyKey = null,
            CancellationToken ct = default)
        {
            // 1. Idempotency Check: if already completed, return existing record
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await FindByIdempotencyKeyAsync(idempotencyKey!, ct).ConfigureAwait(false);
                if (existing != null)
                {
                    return existing;
                }
            }

            if (!_drafts.TryGetValue(saleId, out var draft) || draft.Lines.Count == 0)
            {
                throw new InvalidOperationException($"Sale draft '{saleId}' is empty or does not exist.");
            }

            var now = DateTime.UtcNow;
            var transactionId = Guid.NewGuid().ToString("N");
            var invoiceNumber = "INV-" + now.ToString("yyyyMMdd") + "-" + now.Ticks.ToString().Substring(12);

            long subtotal = 0;
            long discount = 0;
            foreach (var line in draft.Lines)
            {
                subtotal += (long)(line.Quantity * line.UnitPriceMinor);
                discount += line.DiscountMinor;
            }
            long total = subtotal - discount;

            using (var conn = (SqliteConnection)await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var tx = conn.BeginTransaction())
            {
                var txDto = new TransactionDto
                {
                    Id = transactionId,
                    InvoiceNumber = invoiceNumber,
                    PharmacyId = pharmacyId,
                    TerminalId = terminalId,
                    CashierUserId = cashierId,
                    ShiftId = shiftId,
                    PatientId = draft.PatientId,
                    TransactionType = "Sale",
                    TransactionStatus = "Completed",
                    SubtotalMinor = subtotal,
                    DiscountMinor = discount,
                    TotalMinor = total,
                    Currency = "EGP",
                    IdempotencyKey = idempotencyKey,
                    CreatedAt = now.ToString("o"),
                    CompletedAt = now.ToString("o"),
                    IsSynced = false
                };

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;

                    // Insert Transaction
                    cmd.CommandText = @"
                        INSERT INTO pos_transactions (
                            id, invoice_number, pharmacy_id, terminal_id, cashier_user_id, shift_id,
                            patient_id, transaction_type, transaction_status, subtotal_minor, discount_minor,
                            tax_minor, total_minor, currency, idempotency_key, created_at, completed_at, is_synced
                        ) VALUES (
                            @id, @inv, @pharma, @term, @cashier, @shift,
                            @patient, 'Sale', 'Completed', @subtotal, @discount,
                            0, @total, 'EGP', @idemp, @created, @completed, 0
                        );";
                    cmd.Parameters.AddWithValue("@id", txDto.Id);
                    cmd.Parameters.AddWithValue("@inv", txDto.InvoiceNumber);
                    cmd.Parameters.AddWithValue("@pharma", txDto.PharmacyId);
                    cmd.Parameters.AddWithValue("@term", txDto.TerminalId);
                    cmd.Parameters.AddWithValue("@cashier", txDto.CashierUserId);
                    cmd.Parameters.AddWithValue("@shift", txDto.ShiftId);
                    cmd.Parameters.AddWithValue("@patient", (object?)txDto.PatientId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@subtotal", txDto.SubtotalMinor);
                    cmd.Parameters.AddWithValue("@discount", txDto.DiscountMinor);
                    cmd.Parameters.AddWithValue("@total", txDto.TotalMinor);
                    cmd.Parameters.AddWithValue("@idemp", (object?)txDto.IdempotencyKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@created", txDto.CreatedAt);
                    cmd.Parameters.AddWithValue("@completed", txDto.CompletedAt!);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    // Process each line item with FEFO stock deduction
                    foreach (var line in draft.Lines)
                    {
                        var neededQty = line.Quantity;
                        cmd.CommandText = @"
                            SELECT id, remaining_quantity
                            FROM stock_lots
                            WHERE (local_drug_id = @did OR global_drug_id = @did) AND remaining_quantity > 0
                            ORDER BY expiry_date ASC;";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@did", line.DrugId);

                        var lotsToDeduct = new List<(string LotId, decimal Available)>();
                        using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                            {
                                lotsToDeduct.Add((reader.GetString(0), reader.GetDecimal(1)));
                            }
                        }

                        decimal totalAvailable = 0;
                        foreach (var l in lotsToDeduct) totalAvailable += l.Available;

                        if (totalAvailable < neededQty)
                        {
                            throw new InvalidOperationException($"Insufficient stock for '{line.DrugName}'. Requested: {neededQty}, Available: {totalAvailable}");
                        }

                        string? allocatedLotId = null;
                        foreach (var (lotId, available) in lotsToDeduct)
                        {
                            if (neededQty <= 0) break;
                            var deduct = Math.Min(neededQty, available);
                            allocatedLotId = lotId;

                            // Deduct lot
                            cmd.CommandText = "UPDATE stock_lots SET remaining_quantity = remaining_quantity - @deduct WHERE id = @lid;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@deduct", deduct);
                            cmd.Parameters.AddWithValue("@lid", lotId);
                            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                            // Record movement
                            cmd.CommandText = @"
                                INSERT INTO stock_movements (id, lot_id, drug_id, movement_type, quantity, previous_quantity, new_quantity, reference_id, notes, created_at)
                                VALUES (@mid, @lid, @did, 'Sale', @qty, @prev, @newQty, @refId, @notes, @now);";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@mid", Guid.NewGuid().ToString("N"));
                            cmd.Parameters.AddWithValue("@lid", lotId);
                            cmd.Parameters.AddWithValue("@did", line.DrugId);
                            cmd.Parameters.AddWithValue("@qty", -deduct);
                            cmd.Parameters.AddWithValue("@prev", available);
                            cmd.Parameters.AddWithValue("@newQty", available - deduct);
                            cmd.Parameters.AddWithValue("@refId", txDto.InvoiceNumber);
                            cmd.Parameters.AddWithValue("@notes", "Sale invoice " + txDto.InvoiceNumber);
                            cmd.Parameters.AddWithValue("@now", now.ToString("o"));
                            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                            neededQty -= deduct;
                        }

                        // Insert Transaction Item
                        var itemId = Guid.NewGuid().ToString("N");
                        cmd.CommandText = @"
                            INSERT INTO pos_transaction_items (
                                item_id, transaction_id, drug_id, drug_name, package_tier, quantity,
                                base_unit, unit_price_minor, discount_minor, tax_rate, total_line_minor, currency, lot_id
                            ) VALUES (
                                @iid, @tid, @did, @dname, @tier, @qty,
                                'Box', @uprice, @disc, 0, @ltotal, 'EGP', @lotid
                            );";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@iid", itemId);
                        cmd.Parameters.AddWithValue("@tid", txDto.Id);
                        cmd.Parameters.AddWithValue("@did", line.DrugId);
                        cmd.Parameters.AddWithValue("@dname", line.DrugName);
                        cmd.Parameters.AddWithValue("@tier", line.PackageTier);
                        cmd.Parameters.AddWithValue("@qty", line.Quantity);
                        cmd.Parameters.AddWithValue("@uprice", line.UnitPriceMinor);
                        cmd.Parameters.AddWithValue("@disc", line.DiscountMinor);
                        cmd.Parameters.AddWithValue("@ltotal", line.LineTotalMinor);
                        cmd.Parameters.AddWithValue("@lotid", (object?)allocatedLotId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                        txDto.Items.Add(new TransactionItemDto
                        {
                            ItemId = itemId,
                            TransactionId = txDto.Id,
                            DrugId = line.DrugId,
                            DrugName = line.DrugName,
                            Quantity = line.Quantity,
                            UnitPriceMinor = line.UnitPriceMinor,
                            DiscountMinor = line.DiscountMinor,
                            TotalLineMinor = line.LineTotalMinor,
                            LotId = allocatedLotId
                        });
                    }

                    // Record Outbox event for cloud sync
                    var payload = JsonSerializer.Serialize(txDto);
                    cmd.CommandText = @"
                        INSERT INTO outbox_events (id, aggregate_type, aggregate_id, event_type, payload_json, created_at, status)
                        VALUES (@eid, 'Transaction', @aggId, 'SaleCompleted', @payload, @now, 'Pending');";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@eid", Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@aggId", txDto.Id);
                    cmd.Parameters.AddWithValue("@payload", payload);
                    cmd.Parameters.AddWithValue("@now", now.ToString("o"));
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    // Record Audit event
                    cmd.CommandText = @"
                        INSERT INTO audit_events (id, timestamp, user_id, action, entity_type, entity_id, details_json)
                        VALUES (@aid, @now, @uid, 'CompleteSale', 'Transaction', @tid, @details);";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@aid", Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@now", now.ToString("o"));
                    cmd.Parameters.AddWithValue("@uid", cashierId);
                    cmd.Parameters.AddWithValue("@tid", txDto.Id);
                    cmd.Parameters.AddWithValue("@details", "Completed sale " + txDto.InvoiceNumber + " for " + (txDto.TotalMinor / 100.0) + " EGP");
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                tx.Commit();
                _drafts.TryRemove(saleId, out _);
                return txDto;
            }
        }

        public async Task<TransactionDto?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, invoice_number, pharmacy_id, terminal_id, cashier_user_id, shift_id,
                           patient_id, transaction_type, transaction_status, subtotal_minor, discount_minor,
                           tax_minor, total_minor, currency, idempotency_key, created_at, completed_at, is_synced
                    FROM pos_transactions
                    WHERE id = @id OR invoice_number = @id
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", id.Trim());

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var dto = ReadTransactionDto(reader);
                        reader.Close();
                        dto.Items = await GetTransactionItemsAsync(dto.Id, conn, ct).ConfigureAwait(false);
                        return dto;
                    }
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<TransactionDto>> ListRecentTransactionsAsync(int offset = 0, int limit = 50, CancellationToken ct = default)
        {
            var list = new List<TransactionDto>();
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, invoice_number, pharmacy_id, terminal_id, cashier_user_id, shift_id,
                           patient_id, transaction_type, transaction_status, subtotal_minor, discount_minor,
                           tax_minor, total_minor, currency, idempotency_key, created_at, completed_at, is_synced
                    FROM pos_transactions
                    ORDER BY created_at DESC
                    LIMIT " + Math.Max(1, Math.Min(limit, 100)) + " OFFSET " + Math.Max(0, offset) + ";";

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        list.Add(ReadTransactionDto(reader));
                    }
                }
            }

            return list;
        }

        public async Task<TransactionDto> RefundTransactionAsync(string transactionId, string reason, string cashierId, CancellationToken ct = default)
        {
            var txDto = await GetByIdAsync(transactionId, ct).ConfigureAwait(false);
            if (txDto == null) throw new InvalidOperationException($"Transaction '{transactionId}' was not found.");
            if (txDto.TransactionStatus == "Refunded") throw new InvalidOperationException($"Transaction '{transactionId}' is already refunded.");

            using (var conn = (SqliteConnection)await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var tx = conn.BeginTransaction())
            {
                var now = DateTime.UtcNow.ToString("o");
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE pos_transactions SET transaction_status = 'Refunded' WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", txDto.Id);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                    // Return stock for items
                    foreach (var item in txDto.Items)
                    {
                        if (!string.IsNullOrEmpty(item.LotId))
                        {
                            cmd.CommandText = "UPDATE stock_lots SET remaining_quantity = remaining_quantity + @qty WHERE id = @lid;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                            cmd.Parameters.AddWithValue("@lid", item.LotId);
                            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                            cmd.CommandText = @"
                                INSERT INTO stock_movements (id, lot_id, drug_id, movement_type, quantity, previous_quantity, new_quantity, reference_id, notes, created_at)
                                VALUES (@mid, @lid, @did, 'Return', @qty, 0, @qty, @refId, @notes, @now);";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@mid", Guid.NewGuid().ToString("N"));
                            cmd.Parameters.AddWithValue("@lid", item.LotId);
                            cmd.Parameters.AddWithValue("@did", item.DrugId);
                            cmd.Parameters.AddWithValue("@qty", item.Quantity);
                            cmd.Parameters.AddWithValue("@refId", txDto.InvoiceNumber);
                            cmd.Parameters.AddWithValue("@notes", "Refund for invoice " + txDto.InvoiceNumber + ": " + reason);
                            cmd.Parameters.AddWithValue("@now", now);
                            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                        }
                    }

                    // Record Audit
                    cmd.CommandText = @"
                        INSERT INTO audit_events (id, timestamp, user_id, action, entity_type, entity_id, details_json)
                        VALUES (@aid, @now, @uid, 'RefundSale', 'Transaction', @tid, @details);";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@aid", Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@now", now);
                    cmd.Parameters.AddWithValue("@uid", cashierId);
                    cmd.Parameters.AddWithValue("@tid", txDto.Id);
                    cmd.Parameters.AddWithValue("@details", "Refunded sale " + txDto.InvoiceNumber + ". Reason: " + reason);
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                tx.Commit();
                txDto.TransactionStatus = "Refunded";
                return txDto;
            }
        }

        private async Task<TransactionDto?> FindByIdempotencyKeyAsync(string key, CancellationToken ct)
        {
            using (var conn = await _dbProvider.CreateConnectionAsync(ct).ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, invoice_number, pharmacy_id, terminal_id, cashier_user_id, shift_id,
                           patient_id, transaction_type, transaction_status, subtotal_minor, discount_minor,
                           tax_minor, total_minor, currency, idempotency_key, created_at, completed_at, is_synced
                    FROM pos_transactions
                    WHERE idempotency_key = @key
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@key", key);

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var dto = ReadTransactionDto(reader);
                        reader.Close();
                        dto.Items = await GetTransactionItemsAsync(dto.Id, conn, ct).ConfigureAwait(false);
                        return dto;
                    }
                }
            }

            return null;
        }

        private static async Task<List<TransactionItemDto>> GetTransactionItemsAsync(string txId, DbConnection conn, CancellationToken ct)
        {
            var items = new List<TransactionItemDto>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT item_id, transaction_id, drug_id, drug_name, package_tier, quantity,
                           base_unit, unit_price_minor, discount_minor, tax_rate, total_line_minor, currency, lot_id
                    FROM pos_transaction_items
                    WHERE transaction_id = @tid;";
                cmd.Parameters.Add(new SqliteParameter("@tid", txId));

                using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        items.Add(new TransactionItemDto
                        {
                            ItemId = reader.GetString(reader.GetOrdinal("item_id")),
                            TransactionId = reader.GetString(reader.GetOrdinal("transaction_id")),
                            DrugId = reader.GetString(reader.GetOrdinal("drug_id")),
                            DrugName = reader.GetString(reader.GetOrdinal("drug_name")),
                            PackageTier = reader.GetString(reader.GetOrdinal("package_tier")),
                            Quantity = reader.GetDecimal(reader.GetOrdinal("quantity")),
                            BaseUnit = reader.GetString(reader.GetOrdinal("base_unit")),
                            UnitPriceMinor = reader.GetInt64(reader.GetOrdinal("unit_price_minor")),
                            DiscountMinor = reader.GetInt64(reader.GetOrdinal("discount_minor")),
                            TaxRate = reader.GetDecimal(reader.GetOrdinal("tax_rate")),
                            TotalLineMinor = reader.GetInt64(reader.GetOrdinal("total_line_minor")),
                            Currency = reader.GetString(reader.GetOrdinal("currency")),
                            LotId = reader.IsDBNull(reader.GetOrdinal("lot_id")) ? null : reader.GetString(reader.GetOrdinal("lot_id"))
                        });
                    }
                }
            }

            return items;
        }

        private static TransactionDto ReadTransactionDto(DbDataReader reader)
        {
            return new TransactionDto
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                InvoiceNumber = reader.GetString(reader.GetOrdinal("invoice_number")),
                PharmacyId = reader.GetString(reader.GetOrdinal("pharmacy_id")),
                TerminalId = reader.GetString(reader.GetOrdinal("terminal_id")),
                CashierUserId = reader.GetString(reader.GetOrdinal("cashier_user_id")),
                ShiftId = reader.GetString(reader.GetOrdinal("shift_id")),
                PatientId = reader.IsDBNull(reader.GetOrdinal("patient_id")) ? null : reader.GetString(reader.GetOrdinal("patient_id")),
                TransactionType = reader.GetString(reader.GetOrdinal("transaction_type")),
                TransactionStatus = reader.GetString(reader.GetOrdinal("transaction_status")),
                SubtotalMinor = reader.GetInt64(reader.GetOrdinal("subtotal_minor")),
                DiscountMinor = reader.GetInt64(reader.GetOrdinal("discount_minor")),
                TaxMinor = reader.GetInt64(reader.GetOrdinal("tax_minor")),
                TotalMinor = reader.GetInt64(reader.GetOrdinal("total_minor")),
                Currency = reader.GetString(reader.GetOrdinal("currency")),
                IdempotencyKey = reader.IsDBNull(reader.GetOrdinal("idempotency_key")) ? null : reader.GetString(reader.GetOrdinal("idempotency_key")),
                CreatedAt = reader.GetString(reader.GetOrdinal("created_at")),
                CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : reader.GetString(reader.GetOrdinal("completed_at")),
                IsSynced = reader.GetInt32(reader.GetOrdinal("is_synced")) == 1
            };
        }
    }
}
