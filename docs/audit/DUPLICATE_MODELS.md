# Audit: Duplicate Models & Resolution Strategy

During the inspection of the 12 existing SOPs packages, several duplicate or overlapping model definitions were identified. This document details each duplication and the resolution implemented by `Sops.Models`.

---

## 1. Identified Duplications & Conflicts

### 1.1 `Money`
- **Occurrence**: Defined in `Sops.Payments.Models.Money`.
- **Current Shape**: `long AmountMinorUnits`, `string Currency`, arithmetic operators (`+`, `-`, `*`), string formatting.
- **Problem**: Other packages (`Sops.Drugs`, POS transaction modules, billing) also require monetary representations, leading to potential type mismatch or awkward dependencies on `Sops.Payments`.
- **Resolution**:
  - `Sops.Models.Common.Money` is established as the canonical universal financial value object.
  - Matches the exact representation (`AmountMinorUnits`, `Currency`) with identical serialization keys (`amount_minor_units`, `currency`).
  - `Sops.Payments` will preserve its existing type for full backward compatibility and provide implicit conversion to `Sops.Models.Common.Money`.

### 1.2 `DeviceFingerprint`
- **Occurrences**:
  - `Sops.Device.Models.DeviceFingerprint`: Comprehensive raw hardware probe results (Motherboard serial, CPU ID, MAC addresses, Disk drive serials, confidence weights).
  - `Sops.Licensing.Models.DeviceFingerprint`: Simplified representation used for license binding.
- **Problem**: Divergence between the two packages and security concerns regarding raw hardware identifiers leaking over public API boundaries.
- **Resolution**:
  - `Sops.Device` remains the authoritative package for local hardware signal collection.
  - `Sops.Models.Device.DeviceIdentitySummary` provides the redacted public contract (exposes only `DeviceId`, `DeviceCode`, `Platform`, `DeviceName`, and `Status`).
  - No raw hardware serials or unkeyed MAC addresses are present in `Sops.Models`.

### 1.3 `EventEnvelope`
- **Occurrence**: Defined in `Sops.DataLayer.Models.Events.EventEnvelope`.
- **Current Shape**: `Id`, `Topic`, `Source`, `Timestamp`, `PayloadJson`, `Headers`.
- **Problem**: Synchronization, messaging, and audit logging require an event envelope, but coupling directly to `Sops.DataLayer` introduces a UI/client communication dependency.
- **Resolution**:
  - `Sops.Models.Sync.SyncEventEnvelope` provides the pure domain event contract (`EventId`, `AggregateType`, `AggregateId`, `EventType`, `OccurredAtUtc`, `TenantId`, `BranchId`, `PayloadJson`).
  - `Sops.DataLayer` can wrap or adapt `SyncEventEnvelope` into its transport envelope.

### 1.4 `Drug` & `DrugPackage`
- **Occurrence**: Defined in `Sops.Drugs.Models.Drug`.
- **Current Shape**: Contains catalogue information, pricing, dosage forms, and packaging info.
- **Problem**: Multiple frontend and database consumers need the Drug contract, but should not depend on catalogue search indexers or CSV parsers inside `Sops.Drugs`.
- **Resolution**:
  - `Sops.Models.Catalog.Drug` establishes the canonical, lightweight data contract.
  - Value object `DrugPackage` is embedded with positive integer conversion factors.
  - `Sops.Drugs` references `Sops.Models` and operates directly on this canonical definition.

### 1.5 `Payment` vs `BillingPayment`
- **Occurrence**: The term "Payment" was historically overloaded to represent customer cash/card sales at the cash register AND recurring software subscription invoice charges.
- **Resolution**:
  - `Sops.Models.Transactions.Payment`: Represents retail point-of-sale customer tender (tender method, currency amount, tender status, terminal id, receipt number).
  - `Sops.Models.Billing.BillingPayment`: Represents SaaS software billing (subscription plan, tenant id, payment gateway transaction reference, invoice number).
  - These two models are strictly separated into different namespaces and schemas.
