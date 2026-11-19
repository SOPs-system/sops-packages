# Database Mapping: Global PostgreSQL Schemas

This document maps canonical `Sops.Models` contracts to physical PostgreSQL tables in the Global database.

---

## 1. Relational Table Mappings

### 1.1 `catalog_drugs` ⇄ `Sops.Models.Catalog.Drug`

```sql
CREATE TABLE catalog_drugs (
    id UUID PRIMARY KEY,
    sku VARCHAR(64) NOT NULL UNIQUE,
    barcode VARCHAR(64) NOT NULL UNIQUE,
    name_en VARCHAR(256) NOT NULL,
    name_ar VARCHAR(256) NOT NULL,
    generic_name VARCHAR(256) NOT NULL,
    category_id UUID REFERENCES catalog_categories(id),
    dosage_form VARCHAR(32) NOT NULL,
    official_price_minor BIGINT NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EGP',
    is_price_regulated BOOLEAN NOT NULL DEFAULT TRUE,
    requires_prescription BOOLEAN NOT NULL DEFAULT FALSE,
    storage_type VARCHAR(32) NOT NULL DEFAULT 'RoomTemperature',
    packages_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    ingredients_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    status VARCHAR(32) NOT NULL DEFAULT 'Active',
    catalog_revision BIGINT NOT NULL DEFAULT 1,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_catalog_drugs_sku ON catalog_drugs(sku);
CREATE INDEX idx_catalog_drugs_barcode ON catalog_drugs(barcode);
CREATE INDEX idx_catalog_drugs_revision ON catalog_drugs(catalog_revision);
```

#### Field Transformations & Rules:
- `Drug.Packages` is stored directly as a `JSONB` array (`packages_json`), avoiding unneeded 1:N normalized joins for static value objects.
- `official_price_minor` stores the `Money.AmountMinorUnits` as a 64-bit integer (`BIGINT`).
- `currency` stores the ISO 4217 code.

---

### 1.2 `global_patients` ⇄ `Sops.Models.Patients.Patient`

```sql
CREATE TABLE global_patients (
    id UUID PRIMARY KEY,
    national_id_hash VARCHAR(64) NOT NULL UNIQUE, -- SHA-256 HMAC index
    full_name VARCHAR(256) NOT NULL,
    phone_number VARCHAR(32) NOT NULL,
    date_of_birth DATE NULL,
    gender VARCHAR(16) NOT NULL,
    blood_group VARCHAR(8) NULL,
    chronic_conditions JSONB NOT NULL DEFAULT '[]'::jsonb,
    allergies JSONB NOT NULL DEFAULT '[]'::jsonb,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_global_patients_phone ON global_patients(phone_number);
```

---

### 1.3 `global_chains` ⇄ `Sops.Models.Tenancy.Chain`

```sql
CREATE TABLE global_chains (
    id UUID PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    tax_number VARCHAR(64) NOT NULL,
    commercial_registration VARCHAR(64) NOT NULL,
    subscription_tier VARCHAR(32) NOT NULL DEFAULT 'Professional',
    provision_status VARCHAR(32) NOT NULL DEFAULT 'Active',
    tenant_db_connection_token VARCHAR(256) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

### 1.4 `device_registrations` ⇄ `Sops.Models.Device.DeviceIdentitySummary`

```sql
CREATE TABLE device_registrations (
    device_id UUID PRIMARY KEY,
    device_code VARCHAR(32) NOT NULL UNIQUE,
    chain_id UUID NOT NULL REFERENCES global_chains(id),
    pharmacy_id UUID NOT NULL,
    platform VARCHAR(32) NOT NULL,
    device_name VARCHAR(128) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Active',
    registered_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_heartbeat_utc TIMESTAMPTZ NULL
);
```
