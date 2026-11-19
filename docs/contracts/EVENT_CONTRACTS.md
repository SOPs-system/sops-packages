# Contracts: Synchronization & Event Envelopes

Events drive the offline-first synchronization engine connecting local pharmacy terminals to tenant and global cloud services.

---

## 1. Universal Sync Event Envelope (`Sops.Models.Sync.SyncEventEnvelope`)

Wraps any domain mutation or event emitted by a node.

### Structure
- `event_id` (`Guid`): Unique event identifier for idempotency.
- `aggregate_type` (`string`): Aggregate root name (e.g., `"Transaction"`, `"StockLot"`, `"TransferRequest"`).
- `aggregate_id` (`string`): Unique string ID of the modified entity.
- `event_type` (`string`): Fully qualified event name (e.g., `"TransactionCompleted"`, `"StockAdjusted"`).
- `mutation_kind` (`MutationKind`): Enum (`Created`, `Updated`, `Deleted`, `ActionExecuted`).
- `occurred_at_utc` (`DateTimeOffset`): Exact local timestamp when the event took place.
- `tenant_id` (`Guid`): Chain tenant identifier.
- `branch_id` (`Guid`): Originating branch identifier.
- `payload_json` (`string`): Serialized JSON payload matching the canonical schema.
- `schema_version` (`string`): Schema version of the payload (e.g., `"v1"`).

---

## 2. Sync Batch (`Sops.Models.Sync.SyncBatch`)

Represents a compressed transmission of ordered events pushed by a local POS node upon reconnecting to the internet.

### Structure
- `batch_id` (`Guid`): Unique batch ID.
- `branch_id` (`Guid`): Originating pharmacy branch.
- `client_sequence_start` (`long`): Sequence index of first event in batch.
- `client_sequence_end` (`long`): Sequence index of last event in batch.
- `events` (`IReadOnlyList<SyncEventEnvelope>`): Ordered event stream.
- `sent_at_utc` (`DateTimeOffset`): Batch dispatch timestamp.

---

## 3. Record Change / Approval (`Sops.Models.Sync.ChangeRequest`)

Governs operations requiring higher-level manager approval (e.g., manual stock write-offs, large price discounts, credit note issuance).

### Structure
- `request_id` (`Guid`): Unique change request ID.
- `tenant_id` (`Guid`): Chain tenant.
- `branch_id` (`Guid`): Branch where request originated.
- `requested_by_user_id` (`Guid`): Cashier or pharmacist requesting.
- `change_type` (`string`): Action classification (e.g., `"StockAdjustment"`, `"TransactionVoid"`).
- `reason` (`string`): Operational explanation.
- `status` (`ChangeRequestStatus`): Enum (`Pending`, `Approved`, `Rejected`, `Expired`).
- `decision` (`ChangeDecision?`): Outcome details including decision maker and timestamp.
