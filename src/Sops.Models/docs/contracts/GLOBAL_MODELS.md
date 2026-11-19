# Contracts: Global PostgreSQL Master Models

Global PostgreSQL models are authoritative across the entire SOPs multi-tenant ecosystem. They define master data that is shared, replicated, or referenced by all tenants and local pharmacy instances.

---

## 1. Global Drug Catalogue (`Sops.Models.Catalog.Drug`)

The authoritative drug specification. Sourced from regulatory authorities (e.g., Egyptian EDA), distributor pricelists, and master catalogues.

### Schema Fields
- `id` (`Guid`): Globally unique drug identifier.
- `sku` (`string`): Stock Keeping Unit code (unique string, e.g., `"EDA-100234"`).
- `barcode` (`string`): Primary EAN-13 / GS1 barcode for scanning.
- `name_en` (`string`): Official English / Latin trade name.
- `name_ar` (`string`): Official Arabic trade name.
- `generic_name` (`string`): International Nonproprietary Name (INN).
- `category_id` (`Guid?`): Reference to master `DrugCategory`.
- `dosage_form` (`DrugDosageForm`): Enum (Tablet, Capsule, Syrup, Sachet, Injection, Drop, Cream, Gel, Inhaler, Patch, Suppository, Other).
- `official_price` (`Money`): Government-mandated retail price in minor units with currency.
- `is_price_regulated` (`bool`): Whether this drug is subject to strict governmental price caps.
- `requires_prescription` (`bool`): Dispensing safety flag.
- `storage_type` (`StorageType`): Enum (RoomTemperature, Refrigerated, Frozen, ControlledSubstance).
- `packages` (`IReadOnlyList<DrugPackage>`): Embedded value objects defining package tiers and conversion factors.
- `active_ingredients` (`IReadOnlyList<ActiveIngredient>`): Ingredients and concentrations.
- `status` (`DrugStatus`): Enum (Active, Suspended, Discontinued, Recalled).
- `catalog_revision` (`long`): Monotonically increasing revision counter for sync replication.

---

## 2. Global Patient Directory (`Sops.Models.Patients.Patient`)

Authoritative patient identity directory. Used for federated patient lookup, insurance validation, and chronic medication history.

### Schema Fields
- `id` (`Guid`): Globally unique patient ID.
- `national_id_hash` (`string`): Blind cryptographic index of national identity card for search without storing plaintext ID in public tables.
- `full_name` (`string`): Patient legal name.
- `phone_number` (`string`): Primary contact phone.
- `date_of_birth` (`DateOnly?`): Date of birth.
- `gender` (`Gender`): Enum (Male, Female, Unspecified).
- `blood_group` (`string?`): Blood type (e.g., `"A+"`, `"O-"`).
- `chronic_conditions` (`IReadOnlyList<string>`): List of registered chronic diseases (e.g., Hypertension, Diabetes Type 2).
- `allergies` (`IReadOnlyList<string>`): Drug allergy alerts.
- `created_at_utc` (`DateTimeOffset`): Registration timestamp.

---

## 3. Organizations and Chains (`Sops.Models.Tenancy.Chain`)

Master corporate entity representing a pharmacy chain or independent pharmacy group.

### Schema Fields
- `id` (`Guid`): Chain identifier.
- `name` (`string`): Registered legal commercial name.
- `tax_number` (`string`): Official corporate tax registration ID.
- `commercial_registration` (`string`): Chamber of commerce registration number.
- `tier` (`SubscriptionTier`): Enum (Free, Professional, Enterprise, Custom).
- `status` (`TenantProvisionStatus`): Enum (Pending, Active, Suspended, Decommissioned).
- `created_at_utc` (`DateTimeOffset`): Account creation time.

---

## 4. Global Device Registry (`Sops.Models.Device.DeviceIdentitySummary`)

Tracks authorized hardware devices running SOPs software across all branches.

### Schema Fields
- `device_id` (`Guid`): Device identifier.
- `device_code` (`string`): Human-readable Crockford Base32 device code (e.g., `"D7KX-92MA-88P"`).
- `chain_id` (`Guid`): Owning chain.
- `pharmacy_id` (`Guid`): Registered pharmacy branch.
- `platform` (`DevicePlatform`): Operating system (Windows, Linux, macOS, Android, iOS).
- `device_name` (`string`): Friendly workstation hostname.
- `status` (`DeviceStatus`): Enum (PendingActivation, Active, Suspended, Revoked).
- `registered_at_utc` (`DateTimeOffset`): First registration timestamp.
- `last_heartbeat_utc` (`DateTimeOffset?`): Most recent ping.
