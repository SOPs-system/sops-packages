# Contracts: Shared Value Objects & Common Envelopes

Value Objects are immutable, identity-less domain constructs compared by value equality rather than identifier. They are universally shared across Global, Tenant, and Local tiers.

---

## 1. Money (`Sops.Models.Common.Money`)

Financial accuracy is non-negotiable in pharmacy point-of-sale and healthcare systems. Floating-point calculations (`float`, `double`) are strictly prohibited due to rounding drift.

### Structure
- `amount_minor_units` (`long`): Total amount represented in the currency's smallest subunit (e.g., 100.50 EGP = `10050` piasters/cents).
- `currency` (`string`): 3-letter ISO 4217 currency code (e.g., `"EGP"`, `"SAR"`, `"USD"`).

### Behavior & Invariants
- Immutable `record struct` in C#.
- Same-currency arithmetic enforcement (adding EGP to USD throws `InvalidOperationException`).
- Arithmetic operators (`+`, `-`, `*`).
- Formatter helper producing localized currency strings (`100.50 EGP`).
- Factory methods: `FromMinorUnits(long, string)`, `FromDecimal(decimal, string)`.

---

## 2. Quantity (`Sops.Models.Common.Quantity`)

Represents countable or measurable physical inventory and dosage units.

### Structure
- `value` (`decimal`): Numerical count (e.g., `3`, `0.5`, `10`).
- `unit` (`string`): Base unit representation (e.g., `"tablets"`, `"boxes"`, `"ml"`).

---

## 3. Address (`Sops.Models.Common.Address`)

Physical street address for pharmacies, branches, warehouses, and distributors.

### Structure
- `street_line1` (`string`): Street address, building number.
- `street_line2` (`string?`): Floor, suite, apartment, landmark.
- `city` (`string`): City or municipality (e.g., `"Cairo"`, `"Alexandria"`).
- `state_or_governorate` (`string`): Governorate or province (e.g., `"Giza"`).
- `postal_code` (`string?`): Postal code.
- `country_code` (`string`): 2-letter ISO 3166-1 alpha-2 code (e.g., `"EG"`).

---

## 4. Audit Metadata (`Sops.Models.Common.AuditMetadata`)

Universal provenance record attached to persistent domain models.

### Structure
- `created_at_utc` (`DateTimeOffset`): Timestamp of creation.
- `created_by_user_id` (`Guid?`): Identity of creator.
- `updated_at_utc` (`DateTimeOffset?`): Timestamp of last modification.
- `updated_by_user_id` (`Guid?`): Identity of modifier.
- `version` (`long`): Concurrency version / row version sequence.

---

## 5. Result Envelope & Pagination (`Sops.Models.Common`)

Standard wire envelope for API responses and paginated lists.

### ResultEnvelope<T>
- `is_success` (`bool`): High-level outcome flag.
- `data` (`T?`): Payload on success.
- `error` (`ErrorContract?`): Structured error details on failure.
- `correlation_id` (`string`): Distributed trace identifier.

### PageRequest & PageResult<T>
- `page_number` (`int`): 1-based page index.
- `page_size` (`int`): Items per page (max 500).
- `items` (`IReadOnlyList<T>`): Page items.
- `total_count` (`long`): Total matching records.
- `total_pages` (`int`): Total calculated pages.
