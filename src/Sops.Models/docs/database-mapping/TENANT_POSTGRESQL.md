# Database Mapping: Tenant PostgreSQL Schemas

This document maps tenant chain management contracts to physical PostgreSQL tables in the isolated Tenant database.

---

## 1. Relational Table Mappings

### 1.1 `tenant_branches` ⇄ `Sops.Models.Tenancy.Branch`

```sql
CREATE TABLE tenant_branches (
    id UUID PRIMARY KEY,
    chain_id UUID NOT NULL,
    branch_code VARCHAR(32) NOT NULL UNIQUE,
    name VARCHAR(256) NOT NULL,
    is_warehouse BOOLEAN NOT NULL DEFAULT FALSE,
    street_line1 VARCHAR(256) NOT NULL,
    street_line2 VARCHAR(256) NULL,
    city VARCHAR(128) NOT NULL,
    state_or_governorate VARCHAR(128) NOT NULL,
    postal_code VARCHAR(32) NULL,
    country_code VARCHAR(2) NOT NULL DEFAULT 'EG',
    phone VARCHAR(32) NOT NULL,
    manager_name VARCHAR(128) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

### 1.2 `branch_stock_snapshots` ⇄ `Sops.Models.Chain.BranchStockSnapshot`

```sql
CREATE TABLE branch_stock_snapshots (
    branch_id UUID NOT NULL REFERENCES tenant_branches(id),
    drug_id UUID NOT NULL,
    drug_sku VARCHAR(64) NOT NULL,
    quantity_in_base_units BIGINT NOT NULL,
    base_unit_name VARCHAR(32) NOT NULL,
    snapshot_revision BIGINT NOT NULL,
    snapshot_timestamp_utc TIMESTAMPTZ NOT NULL,
    last_synchronized_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (branch_id, drug_id)
);

CREATE INDEX idx_snapshots_drug ON branch_stock_snapshots(drug_id);
```

---

### 1.3 `transfer_requests` & `transfer_lines` ⇄ `Sops.Models.Chain.TransferRequest`

```sql
CREATE TABLE transfer_requests (
    id UUID PRIMARY KEY,
    transfer_number VARCHAR(64) NOT NULL UNIQUE,
    tenant_id UUID NOT NULL,
    source_branch_id UUID NOT NULL REFERENCES tenant_branches(id),
    destination_branch_id UUID NOT NULL REFERENCES tenant_branches(id),
    status VARCHAR(32) NOT NULL DEFAULT 'Requested',
    requested_by_user_id UUID NOT NULL,
    approved_by_user_id UUID NULL,
    dispatched_at_utc TIMESTAMPTZ NULL,
    received_at_utc TIMESTAMPTZ NULL,
    notes TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE transfer_lines (
    line_id UUID PRIMARY KEY,
    transfer_id UUID NOT NULL REFERENCES transfer_requests(id) ON DELETE CASCADE,
    drug_id UUID NOT NULL,
    drug_name VARCHAR(256) NOT NULL,
    requested_quantity DECIMAL(18,4) NOT NULL,
    requested_unit VARCHAR(32) NOT NULL,
    approved_quantity DECIMAL(18,4) NULL,
    dispatched_quantity DECIMAL(18,4) NULL,
    received_quantity DECIMAL(18,4) NULL,
    batch_number VARCHAR(64) NULL,
    expiry_date DATE NULL
);

CREATE INDEX idx_transfer_requests_source ON transfer_requests(source_branch_id);
CREATE INDEX idx_transfer_requests_dest ON transfer_requests(destination_branch_id);
```
