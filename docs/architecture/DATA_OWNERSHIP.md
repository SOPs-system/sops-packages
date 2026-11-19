# Sops.Models — Data Ownership & Authority Matrix

## 1. Principle of Single Authority

In a distributed, offline-first pharmacy architecture, data conflict is prevented by assigning strict, unambiguous **single authority** to specific domain entities.

A node (Global Cloud, Tenant Cloud, or Local POS) is either:
- **Authoritative**: Owns the state, validates mutations, generates audit entries, and issues version revisions.
- **Projected / Read-Only**: Holds a synchronized or cached replica with clear snapshot metadata (`LastSynchronizedAt`, `SnapshotRevision`).

---

## 2. Master Domain Ownership Matrix

| Domain Model | Canonical Contract | Authoritative Master | Synchronization Target | Snapshot Semantics |
|---|---|---|---|---|
| **Drug Catalogue** | `Sops.Models.Catalog.Drug` | **Global Cloud (PostgreSQL)** | Tenant Cloud, Local POS | Versioned (`CatalogRevision`), cached locally for offline lookups. Official price cannot be locally overwritten. |
| **Drug Package** | `Sops.Models.Catalog.DrugPackage` | **Global Cloud (PostgreSQL)** | Local POS | Embedded Value Object in Drug. Fixed integer conversion factors. |
| **Local Drug Inventory** | `Sops.Models.Inventory.LocalDrug` | **Local Pharmacy (SQLCipher)** | Tenant Cloud (Snapshot) | Locally owned. Contains local operational cost, custom shelf location, and min/max stock thresholds. |
| **Stock Lot / Batch** | `Sops.Models.Inventory.StockLot` | **Local Pharmacy (SQLCipher)** | Local POS Only | Locally authoritative. Batch number, physical expiry date, FEFO order, current quantity in base units. |
| **Chain Stock Snapshot** | `Sops.Models.Chain.BranchStockSnapshot` | **Tenant Cloud (PostgreSQL)** | Chain Management Portal | Projected / Read-Only. Aggregated from local pharmacy sync batches. Stale timestamps prevent assuming live availability. |
| **Patient Identity** | `Sops.Models.Patients.Patient` | **Global Cloud (PostgreSQL)** | Local POS (On-Demand) | Globally unique. National ID and demographic master. |
| **Local Patient Ledger** | `Sops.Models.Patients.LocalPatientReference` | **Local Pharmacy (SQLCipher)** | Local POS Only | Local debt/credit, local pharmacy notes, and loyalty score. Never synced to global patient directory. |
| **POS Transaction** | `Sops.Models.Transactions.Transaction` | **Local Pharmacy (SQLCipher)** | Tenant Cloud (Audit / Sync) | Locally authoritative. Must complete offline without cloud availability. Includes line items and tender. |
| **POS Payment** | `Sops.Models.Transactions.Payment` | **Local Pharmacy (SQLCipher)** | Tenant Cloud (Settlement) | Retail point-of-sale payment (Cash, Visa, Split, Voucher). Completely distinct from SaaS `BillingPayment`. |
| **SaaS Billing Payment** | `Sops.Models.Billing.BillingPayment` | **Global Cloud (PostgreSQL)** | Tenant Account / Billing Portal | Software subscription invoice payments, Paymob gateway webhooks, recurring license fees. |
| **Inter-Branch Transfer** | `Sops.Models.Chain.TransferRequest` | **Tenant Cloud (PostgreSQL)** | Origin & Destination Branches | Coordinated multi-stage lifecycle: `Draft` -> `Requested` -> `Approved` -> `Dispatched` -> `Received` -> `Completed`. |
| **Device Registration** | `Sops.Models.Device.DeviceIdentitySummary` | **Global Cloud (PostgreSQL)** | Local POS (License cert) | Hardware claims, activation status, branch binding. Raw device probes remain local in `Sops.Device`. |
| **License Claims** | `Sops.Models.Licensing.LicenseClaims` | **Global Cloud (PostgreSQL)** | Local POS (Signed token) | Cryptographically signed entitlement claims, max branch seats, feature flags. |

---

## 3. Offline Safety & Stock Movement Guarantees

1. **Local Stock Authority**:
   - Stock deduction occurs **immediately and atomically** in the local SQLCipher database upon POS transaction completion.
   - A cloud approval or chain transfer request NEVER directly decrements physical inventory until the local branch records physical dispatch.
2. **Anonymous Checkout**:
   - Transactions support anonymous walk-in patients (`PatientId` is nullable). No patient registration is ever mandated for OTC dispensing.
3. **Price Integrity**:
   - The government-mandated price (`OfficialPrice`) in `Drug` is immutable by local operators.
   - Historical sales transactions store the exact, frozen price at transaction time (`TransactionItem.UnitPrice`), preserving historical financial accuracy regardless of future price changes.
