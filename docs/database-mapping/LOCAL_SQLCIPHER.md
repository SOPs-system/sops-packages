# Database Mapping: Local SQLCipher Schemas

This document maps local pharmacy operations and POS transaction contracts to physical tables in the encrypted local SQLCipher SQLite database.

---

## 1. Local Database Tables

### 1.1 `local_drugs` ⇄ `Sops.Models.Inventory.LocalDrug`

```sql
CREATE TABLE local_drugs (
    id TEXT PRIMARY KEY, -- UUID string
    global_drug_id TEXT NOT NULL,
    pharmacy_id TEXT NOT NULL,
    local_cost_minor INTEGER NOT NULL,
    custom_sell_price_minor INTEGER NULL,
    currency TEXT NOT NULL DEFAULT 'EGP',
    storage_location_id TEXT NULL,
    min_stock INTEGER NOT NULL DEFAULT 5,
    max_stock INTEGER NOT NULL DEFAULT 100,
    is_active_locally INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

CREATE INDEX idx_local_drugs_global ON local_drugs(global_drug_id);
```

---

### 1.2 `stock_lots` ⇄ `Sops.Models.Inventory.StockLot`

```sql
CREATE TABLE stock_lots (
    id TEXT PRIMARY KEY,
    local_drug_id TEXT NOT NULL REFERENCES local_drugs(id),
    global_drug_id TEXT NOT NULL,
    batch_number TEXT NOT NULL,
    expiry_date TEXT NOT NULL, -- YYYY-MM-DD
    received_quantity DECIMAL NOT NULL,
    remaining_quantity DECIMAL NOT NULL,
    reserved_quantity DECIMAL NOT NULL DEFAULT 0,
    base_unit TEXT NOT NULL,
    purchase_price_minor INTEGER NOT NULL,
    currency TEXT NOT NULL DEFAULT 'EGP',
    distributor_id TEXT NULL,
    received_date TEXT NOT NULL
);

CREATE INDEX idx_stock_lots_drug ON stock_lots(global_drug_id);
CREATE INDEX idx_stock_lots_expiry ON stock_lots(expiry_date);
```

---

### 1.3 `pos_transactions` & `pos_transaction_items` ⇄ `Sops.Models.Transactions.Transaction`

```sql
CREATE TABLE pos_transactions (
    id TEXT PRIMARY KEY,
    invoice_number TEXT NOT NULL UNIQUE,
    pharmacy_id TEXT NOT NULL,
    terminal_id TEXT NOT NULL,
    cashier_user_id TEXT NOT NULL,
    shift_id TEXT NOT NULL,
    patient_id TEXT NULL, -- Nullable for anonymous checkout
    transaction_type TEXT NOT NULL DEFAULT 'Sale',
    transaction_status TEXT NOT NULL DEFAULT 'Completed',
    subtotal_minor INTEGER NOT NULL,
    discount_minor INTEGER NOT NULL DEFAULT 0,
    tax_minor INTEGER NOT NULL DEFAULT 0,
    total_minor INTEGER NOT NULL,
    currency TEXT NOT NULL DEFAULT 'EGP',
    created_at TEXT NOT NULL,
    completed_at TEXT NULL,
    is_synced INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE pos_transaction_items (
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

CREATE INDEX idx_transactions_shift ON pos_transactions(shift_id);
CREATE INDEX idx_transactions_synced ON pos_transactions(is_synced);
```

---

### 1.4 `pos_shifts` ⇄ `Sops.Models.Transactions.Shift`

```sql
CREATE TABLE pos_shifts (
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
    status TEXT NOT NULL DEFAULT 'Open',
    opened_at TEXT NOT NULL,
    closed_at TEXT NULL
);
```
