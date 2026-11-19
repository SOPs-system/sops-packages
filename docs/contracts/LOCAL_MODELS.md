# Contracts: Local SQLCipher Models (Pharmacy POS Operations)

Local SQLCipher models govern the local operations of a brick-and-mortar pharmacy branch. They are authoritative on local operational state and operate 100% offline.

---

## 1. Local Drug Profile (`Sops.Models.Inventory.LocalDrug`)

Represents the branch-specific settings and operational costs for a master catalogue drug.

### Schema Fields
- `id` (`Guid`): Local record ID.
- `global_drug_id` (`Guid`): Reference to master `Sops.Models.Catalog.Drug`.
- `pharmacy_id` (`Guid`): Local branch identifier.
- `local_cost_price` (`Money`): Weighted average purchasing cost in minor units.
- `custom_selling_price` (`Money?`): Local selling price (must not exceed `OfficialPrice` if regulated).
- `storage_location_id` (`Guid?`): Reference to shelf, aisle, or refrigerator (`StorageLocation`).
- `minimum_stock_threshold` (`int`): Reorder warning level.
- `maximum_stock_threshold` (`int`): Maximum shelf capacity.
- `is_active_locally` (`bool`): Whether this pharmacy stocks this item.

---

## 2. Stock Lot / Batch (`Sops.Models.Inventory.StockLot`)

Tracks physical inventory lots, batches, and expiration dates. Authoritative for physical stock counting and dispensing.

### Schema Fields
- `id` (`Guid`): Unique lot identifier.
- `local_drug_id` (`Guid`): Associated local drug.
- `global_drug_id` (`Guid`): Master catalogue reference.
- `batch_number` (`string`): Manufacturer lot/batch number (e.g., `"B240911A"`).
- `expiry_date` (`DateOnly`): Expiration date.
- `received_quantity` (`Quantity`): Total received base units.
- `remaining_quantity` (`Quantity`): Current physical count in base units.
- `reserved_quantity` (`Quantity`): Units held in pending customer orders.
- `purchase_price` (`Money`): Invoice cost for this specific lot.
- `distributor_id` (`Guid?`): Supplying distributor.
- `received_date_utc` (`DateTimeOffset`): Date received into inventory.

---

## 3. Retail POS Transaction (`Sops.Models.Transactions.Transaction`)

The core point-of-sale sales or return record. Created and completed locally at the workstation.

### Schema Fields
- `id` (`Guid`): Unique transaction ID.
- `invoice_number` (`string`): Sequential fiscal receipt number (e.g., `"INV-2026-009842"`).
- `pharmacy_id` (`Guid`): Dispensing branch.
- `terminal_id` (`Guid`): Workstation POS device.
- `cashier_user_id` (`Guid`): Staff member executing the sale.
- `shift_id` (`Guid`): Current active cashier shift.
- `patient_id` (`Guid?`): Optional patient link (NULL for anonymous walk-ins).
- `type` (`TransactionType`): Enum (Sale, Return, Exchange, Void).
- `status` (`TransactionStatus`): Enum (Draft, Completed, Voided, Suspended).
- `subtotal` (`Money`): Pre-tax, pre-discount amount.
- `discount_amount` (`Money`): Applied promotional or customer discount.
- `tax_amount` (`Money`): Sales tax / VAT.
- `total_amount` (`Money`): Final customer charge.
- `items` (`IReadOnlyList<TransactionItem>`): Line items sold.
- `payments` (`IReadOnlyList<Payment>`): Tender split (Cash, Card, Voucher).
- `created_at_utc` (`DateTimeOffset`): Timestamp of creation.
- `completed_at_utc` (`DateTimeOffset?`): Timestamp of transaction completion.

---

## 4. Transaction Item (`Sops.Models.Transactions.TransactionItem`)

Individual line item within a sale or return.

### Schema Fields
- `item_id` (`Guid`): Line identifier.
- `drug_id` (`Guid`): Master catalogue drug ID.
- `drug_name` (`string`): Snapshot of drug name at transaction time.
- `package_tier` (`string`): Packaging tier sold (e.g., `"Box"`, `"Strip"`, `"Unit"`).
- `quantity` (`Quantity`): Quantity in base units sold.
- `unit_price` (`Money`): Transaction-time selling price per unit.
- `discount_amount` (`Money`): Line item discount.
- `tax_rate` (`decimal`): Tax rate applied.
- `total_line_amount` (`Money`): Final line total.
- `lot_id` (`Guid?`): Allocated stock lot for FEFO tracking.

---

## 5. Cashier Shift (`Sops.Models.Transactions.Shift`)

Tracks cash drawer sessions and operational cashier reconciliation.

### Schema Fields
- `id` (`Guid`): Shift identifier.
- `shift_number` (`int`): Sequential shift count for the terminal.
- `pharmacy_id` (`Guid`): Branch ID.
- `terminal_id` (`Guid`): POS device ID.
- `cashier_user_id` (`Guid`): Cashier on duty.
- `opening_cash` (`Money`): Cash float at shift start.
- `closing_cash` (`Money?`): Physical cash counted at shift end.
- `expected_cash` (`Money?`): Calculated cash from opening float + cash sales - cash refunds.
- `cash_difference` (`Money?`): Shortage or surplus (`closing_cash - expected_cash`).
- `status` (`ShiftStatus`): Enum (Open, Closed, Reconciled).
- `opened_at_utc` (`DateTimeOffset`): Shift start time.
- `closed_at_utc` (`DateTimeOffset?`): Shift end time.
