using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Sops.Pharmacy.Server.Configuration;

namespace Sops.Pharmacy.Server.Persistence
{
    public class DatabaseMigrator
    {
        private readonly SqlCipherDatabaseProvider _provider;
        private readonly PharmacyServerSettings _settings;

        public DatabaseMigrator(SqlCipherDatabaseProvider provider, PharmacyServerSettings settings)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            _settings.EnsureDirectories();

            using (var conn = await _provider.OpenSqliteConnectionAsync(cancellationToken).ConfigureAwait(false))
            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;

                    // 1. Schema Migrations table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS schema_migrations (
                            version INTEGER PRIMARY KEY,
                            applied_at TEXT NOT NULL,
                            description TEXT NOT NULL
                        );";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    // Check if v1 is applied
                    cmd.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = 1;";
                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

                    if (count == 0)
                    {
                        cmd.CommandText = GetV1MigrationSql();
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                        // Seed default admin user if absent
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO local_users (id, username, display_name, password_hash, role, is_active, created_at)
                            VALUES ('user_admin_001', 'admin', 'System Administrator', @pwdHash, 'Admin', 1, @now);";
                        cmd.Parameters.AddWithValue("@pwdHash", HashPassword("admin123"));
                        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                        // Seed local branch configuration
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO local_users (id, username, display_name, password_hash, role, is_active, created_at)
                            VALUES ('user_cashier_001', 'cashier1', 'Primary Cashier', @cashierHash, 'Cashier', 1, @now);";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@cashierHash", HashPassword("cashier123"));
                        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                        // Record migration
                        cmd.CommandText = @"
                            INSERT INTO schema_migrations (version, applied_at, description)
                            VALUES (1, @now, 'Initial authoritative local SQLCipher schema');";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                tx.Commit();
            }
        }

        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "_sops_salt_2026"));
                return Convert.ToBase64String(bytes);
            }
        }

        private static string GetV1MigrationSql()
        {
            return @"
                -- 1. Local Drugs Catalog
                CREATE TABLE IF NOT EXISTS local_drugs (
                    id TEXT PRIMARY KEY,
                    global_drug_id TEXT NOT NULL,
                    trade_name TEXT NOT NULL,
                    trade_name_ar TEXT NOT NULL,
                    generic_name TEXT NOT NULL,
                    barcode TEXT NOT NULL,
                    sku TEXT NOT NULL UNIQUE,
                    dosage_form TEXT NOT NULL,
                    strength TEXT NOT NULL,
                    public_price_minor INTEGER NOT NULL,
                    local_cost_minor INTEGER NOT NULL,
                    custom_sell_price_minor INTEGER NULL,
                    units_per_pack INTEGER NOT NULL DEFAULT 1,
                    currency TEXT NOT NULL DEFAULT 'EGP',
                    storage_location_id TEXT NULL,
                    min_stock INTEGER NOT NULL DEFAULT 5,
                    max_stock INTEGER NOT NULL DEFAULT 100,
                    is_active_locally INTEGER NOT NULL DEFAULT 1,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_local_drugs_global ON local_drugs(global_drug_id);
                CREATE INDEX IF NOT EXISTS idx_local_drugs_barcode ON local_drugs(barcode);
                CREATE INDEX IF NOT EXISTS idx_local_drugs_trade_name ON local_drugs(trade_name);

                -- 2. Stock Lots & Batches
                CREATE TABLE IF NOT EXISTS stock_lots (
                    id TEXT PRIMARY KEY,
                    local_drug_id TEXT NOT NULL REFERENCES local_drugs(id),
                    global_drug_id TEXT NOT NULL,
                    batch_number TEXT NOT NULL,
                    expiry_date TEXT NOT NULL,
                    received_quantity DECIMAL NOT NULL,
                    remaining_quantity DECIMAL NOT NULL,
                    reserved_quantity DECIMAL NOT NULL DEFAULT 0,
                    base_unit TEXT NOT NULL,
                    purchase_price_minor INTEGER NOT NULL,
                    currency TEXT NOT NULL DEFAULT 'EGP',
                    distributor_id TEXT NULL,
                    received_date TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_stock_lots_drug ON stock_lots(global_drug_id);
                CREATE INDEX IF NOT EXISTS idx_stock_lots_expiry ON stock_lots(expiry_date);

                -- 3. Stock Movements (Audit log of inventory changes)
                CREATE TABLE IF NOT EXISTS stock_movements (
                    id TEXT PRIMARY KEY,
                    lot_id TEXT NOT NULL REFERENCES stock_lots(id),
                    drug_id TEXT NOT NULL,
                    movement_type TEXT NOT NULL, -- Sale, Return, Adjustment, Purchase, Transfer
                    quantity DECIMAL NOT NULL,
                    previous_quantity DECIMAL NOT NULL,
                    new_quantity DECIMAL NOT NULL,
                    reference_id TEXT NULL,
                    notes TEXT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_stock_movements_lot ON stock_movements(lot_id);

                -- 4. POS Shifts
                CREATE TABLE IF NOT EXISTS pos_shifts (
                    id TEXT PRIMARY KEY,
                    shift_number INTEGER NOT NULL,
                    pharmacy_id TEXT NOT NULL,
                    terminal_id TEXT NOT NULL,
                    cashier_user_id TEXT NOT NULL,
                    opening_cash_minor INTEGER NOT NULL,
                    closing_cash_minor INTEGER NULL,
                    expected_cash_minor INTEGER NULL,
                    cash_diff_minor INTEGER NULL,
                    currency TEXT NOT NULL DEFAULT 'EGP',
                    status TEXT NOT NULL DEFAULT 'Open', -- Open, Closed
                    opened_at TEXT NOT NULL,
                    closed_at TEXT NULL
                );

                -- 5. POS Transactions & Invoices
                CREATE TABLE IF NOT EXISTS pos_transactions (
                    id TEXT PRIMARY KEY,
                    invoice_number TEXT NOT NULL UNIQUE,
                    pharmacy_id TEXT NOT NULL,
                    terminal_id TEXT NOT NULL,
                    cashier_user_id TEXT NOT NULL,
                    shift_id TEXT NOT NULL,
                    patient_id TEXT NULL,
                    transaction_type TEXT NOT NULL DEFAULT 'Sale', -- Sale, Refund, Void
                    transaction_status TEXT NOT NULL DEFAULT 'Completed', -- Draft, Completed, Suspended, Cancelled
                    subtotal_minor INTEGER NOT NULL,
                    discount_minor INTEGER NOT NULL DEFAULT 0,
                    tax_minor INTEGER NOT NULL DEFAULT 0,
                    total_minor INTEGER NOT NULL,
                    currency TEXT NOT NULL DEFAULT 'EGP',
                    idempotency_key TEXT NULL UNIQUE,
                    created_at TEXT NOT NULL,
                    completed_at TEXT NULL,
                    is_synced INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_transactions_shift ON pos_transactions(shift_id);
                CREATE INDEX IF NOT EXISTS idx_transactions_synced ON pos_transactions(is_synced);

                -- 6. POS Transaction Items
                CREATE TABLE IF NOT EXISTS pos_transaction_items (
                    item_id TEXT PRIMARY KEY,
                    transaction_id TEXT NOT NULL REFERENCES pos_transactions(id) ON DELETE CASCADE,
                    drug_id TEXT NOT NULL,
                    drug_name TEXT NOT NULL,
                    package_tier TEXT NOT NULL DEFAULT 'Unit',
                    quantity DECIMAL NOT NULL,
                    base_unit TEXT NOT NULL,
                    unit_price_minor INTEGER NOT NULL,
                    discount_minor INTEGER NOT NULL DEFAULT 0,
                    tax_rate DECIMAL NOT NULL DEFAULT 0,
                    total_line_minor INTEGER NOT NULL,
                    currency TEXT NOT NULL DEFAULT 'EGP',
                    lot_id TEXT NULL
                );

                -- 7. Registered Terminals
                CREATE TABLE IF NOT EXISTS registered_terminals (
                    id TEXT PRIMARY KEY,
                    device_name TEXT NOT NULL,
                    device_type TEXT NOT NULL, -- POS, Mobile, Tablet, Manager
                    hardware_fingerprint TEXT NOT NULL,
                    ip_address TEXT NULL,
                    status TEXT NOT NULL DEFAULT 'Approved', -- Pending, Approved, Revoked
                    last_seen_at TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                -- 8. Local Users & Roles
                CREATE TABLE IF NOT EXISTS local_users (
                    id TEXT PRIMARY KEY,
                    username TEXT NOT NULL UNIQUE,
                    display_name TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    role TEXT NOT NULL DEFAULT 'Cashier', -- Admin, Pharmacist, Cashier
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at TEXT NOT NULL
                );

                -- 9. Audit Events
                CREATE TABLE IF NOT EXISTS audit_events (
                    id TEXT PRIMARY KEY,
                    timestamp TEXT NOT NULL,
                    user_id TEXT NULL,
                    action TEXT NOT NULL,
                    entity_type TEXT NOT NULL,
                    entity_id TEXT NULL,
                    details_json TEXT NULL,
                    ip_address TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_events(timestamp);

                -- 10. Persistent Outbox & Inbox for Offline-first Sync
                CREATE TABLE IF NOT EXISTS outbox_events (
                    id TEXT PRIMARY KEY,
                    aggregate_type TEXT NOT NULL,
                    aggregate_id TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    dispatched_at TEXT NULL,
                    retry_count INTEGER NOT NULL DEFAULT 0,
                    status TEXT NOT NULL DEFAULT 'Pending' -- Pending, Dispatched, Failed
                );
                CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox_events(status);

                CREATE TABLE IF NOT EXISTS inbox_events (
                    id TEXT PRIMARY KEY,
                    aggregate_type TEXT NOT NULL,
                    aggregate_id TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    received_at TEXT NOT NULL,
                    processed_at TEXT NULL,
                    status TEXT NOT NULL DEFAULT 'Pending'
                );

                -- 11. Sync Checkpoints & Conflicts
                CREATE TABLE IF NOT EXISTS sync_checkpoints (
                    stream_name TEXT PRIMARY KEY,
                    last_synced_sequence INTEGER NOT NULL,
                    last_synced_timestamp TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sync_conflicts (
                    id TEXT PRIMARY KEY,
                    entity_type TEXT NOT NULL,
                    entity_id TEXT NOT NULL,
                    local_data_json TEXT NOT NULL,
                    remote_data_json TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'Unresolved',
                    created_at TEXT NOT NULL
                );

                -- 12. Backup History
                CREATE TABLE IF NOT EXISTS backup_history (
                    id TEXT PRIMARY KEY,
                    file_path TEXT NOT NULL,
                    size_bytes INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    checksum TEXT NOT NULL,
                    is_verified INTEGER NOT NULL DEFAULT 0
                );";
        }
    }
}
