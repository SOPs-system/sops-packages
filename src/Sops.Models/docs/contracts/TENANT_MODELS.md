# Contracts: Tenant PostgreSQL Models (Chain Management)

Tenant PostgreSQL databases house chain-level operational management, branch hierarchies, aggregated KPI analytics, and inter-branch logistics workflows.

---

## 1. Branch Directory (`Sops.Models.Tenancy.Branch`)

Represents an operating retail branch or central warehouse within a tenant chain.

### Schema Fields
- `id` (`Guid`): Unique branch ID.
- `chain_id` (`Guid`): Owning chain.
- `branch_code` (`string`): Short organizational code (e.g., `"BR-001"`, `"WH-CAIRO"`).
- `name` (`string`): Branch commercial name.
- `is_warehouse` (`bool`): Whether this branch acts as a distribution hub.
- `address` (`Address`): Physical location (Street, City, State, PostalCode, Country).
- `phone` (`string`): Contact phone.
- `manager_name` (`string`): Responsible supervising pharmacist.
- `is_active` (`bool`): Active branch status flag.

---

## 2. Chain Stock Snapshot (`Sops.Models.Chain.BranchStockSnapshot`)

Projected stock view aggregating inventory levels reported by physical branches. Used for chain-wide stock search and inventory rebalancing.

> **CRITICAL SEMANTICS**: This model is a **projected snapshot**, NOT authoritative live stock. Local sales take precedence.

### Schema Fields
- `branch_id` (`Guid`): Source branch ID.
- `drug_id` (`Guid`): Master drug ID.
- `drug_sku` (`string`): Drug SKU for rapid display.
- `quantity_in_base_units` (`long`): Reported stock quantity.
- `base_unit_name` (`string`): Unit name (e.g., `"Tablet"`, `"Vial"`).
- `snapshot_revision` (`long`): Revision sequence counter from branch sync.
- `snapshot_timestamp_utc` (`DateTimeOffset`): Exact point-in-time when branch took snapshot.
- `last_synchronized_utc` (`DateTimeOffset`): Timestamp when cloud recorded the snapshot.
- `is_stale` (`bool`): Calculated flag indicating if synchronization is delayed.

---

## 3. Inter-Branch Transfer Workflow (`Sops.Models.Chain.TransferRequest`)

Governs the controlled transfer of physical inventory between two branches within the same chain.

### Lifecycle State Machine
```text
Draft ──> Requested ──> Approved ──> Dispatched ──> Received ──> Completed
  │           │           │             │
  └───> Cancelled <───────┴─────────────┘
```

### Schema Fields
- `id` (`Guid`): Transfer request identifier.
- `transfer_number` (`string`): Human-readable reference (e.g., `"TR-2026-00451"`).
- `tenant_id` (`Guid`): Chain tenant.
- `source_branch_id` (`Guid`): Origin branch fulfilling the stock.
- `destination_branch_id` (`Guid`): Requesting branch receiving the stock.
- `status` (`TransferStatus`): Enum (Draft, Requested, Approved, Rejected, Dispatched, InTransit, Received, Completed, Cancelled).
- `lines` (`IReadOnlyList<TransferLine>`): Transfer line items.
- `requested_by_user_id` (`Guid`): Staff member requesting.
- `approved_by_user_id` (`Guid?`): Staff member approving.
- `dispatched_at_utc` (`DateTimeOffset?`): Physical dispatch timestamp.
- `received_at_utc` (`DateTimeOffset?`): Physical receipt timestamp.
- `notes` (`string?`): Reason or delivery notes.

---

## 4. Transfer Line (`Sops.Models.Chain.TransferLine`)

### Schema Fields
- `line_id` (`Guid`): Unique line identifier.
- `drug_id` (`Guid`): Drug being transferred.
- `drug_name` (`string`): Drug trade name.
- `requested_quantity` (`Quantity`): Quantity requested in base units.
- `approved_quantity` (`Quantity?`): Quantity approved by source branch.
- `dispatched_quantity` (`Quantity?`): Actual quantity sent.
- `received_quantity` (`Quantity?`): Actual quantity received.
- `batch_number` (`string?`): Lot batch number dispatched.
- `expiry_date` (`DateOnly?`): Dispatched lot expiry.
