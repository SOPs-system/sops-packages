# Audit: Historical Model Reconciliation & Mapping

This document audits historical models from legacy local SQLCipher, global PostgreSQL, and chain PostgreSQL sources, detailing how each is reconciled into `Sops.Models`.

---

## 1. Master Historical Reconciliation Table

| Historical Concept | Legacy Location / Shape | Target Domain in `Sops.Models` | Target Classification | Reconciliation Strategy |
|---|---|---|---|---|
| **Drug** | Legacy local database with mixed prices and loose barcode | `Sops.Models.Catalog.Drug` | Shared Master Contract (Global authoritative) | Standardize SKU, primary barcode, official public price, package definitions. Separate local operational cost into `LocalDrug`. |
| **DrugPackage** | Independent table with individual barcodes in some legacy apps | `Sops.Models.Catalog.DrugPackage` | Value Object (Embedded in Drug) | Embed as value objects within `Drug.Packages`. Conversion factors strictly positive integers. No independent barcode table. |
| **ActiveIngredient** | Separate normalized relational tables (`Drug_Ingredients`, etc.) | `Sops.Models.Catalog.ActiveIngredient` | Value Object (Embedded in Drug) | Flatten into `Drug.ActiveIngredients` collection to simplify catalogue querying. |
| **StockLot** | Local database table tracking batch numbers and expiration dates | `Sops.Models.Inventory.StockLot` | Local Authoritative Entity | Model base unit stock, batch/lot number, expiry date, FEFO priority. Authoritative strictly in local SQLCipher. |
| **LocalDrug** | Legacy combined drug & inventory rows | `Sops.Models.Inventory.LocalDrug` | Local Entity & Snapshot | Separates local pharmacy overrides (custom shelf location, local cost, reorder level) from global drug master data. |
| **Transaction** | Local sales table with mixed currency and raw doubles | `Sops.Models.Transactions.Transaction` | Local Authoritative Transaction | Uses `Money` (integer minor units). Supports anonymous checkout (`PatientId?` nullable), cashier shift id, and line items. |
| **TransactionItem** | Line items table in sales database | `Sops.Models.Transactions.TransactionItem` | Local Line Item Contract | Freezes transaction-time unit price and discount; records quantity in base units and packaging tier sold. |
| **Payment (POS)** | Overloaded payment table handling retail tender and online transactions | `Sops.Models.Transactions.Payment` | Local Retail Tender | Strictly restricted to point-of-sale customer tender (Cash, Card, Voucher, Split). Separated from SaaS billing. |
| **BillingPayment** | Mixed with retail payments in legacy system | `Sops.Models.Billing.BillingPayment` | Global SaaS Billing | Dedicated contract for software subscription fees, chain seat licensing, and payment gateway webhooks. |
| **Shift** | Local cashier shift table | `Sops.Models.Transactions.Shift` | Local Operational Contract | Cashier ID, terminal ID, opening cash, closing cash, expected vs actual cash differences, settlement timestamps. |
| **Settlement** | Daily accounting aggregation | `Sops.Models.Transactions.Settlement` | Local Financial Summary | Aggregates completed shifts, payment method totals, returns, and cash drop amounts. |
| **Patient** | Legacy local patient table with unredacted medical notes | `Sops.Models.Patients.Patient` | Global Master Directory | Globally unique patient identity (`PatientId`), encrypted demographic summary. Redacts sensitive medical data. |
| **LocalPatientReference**| Local patient account with credit/debt | `Sops.Models.Patients.LocalPatientReference` | Local Pharmacy Ledger | Local credit limit, outstanding pharmacy debt, loyalty points. Strictly local to the dispensing branch. |
| **Chain / Organization**| Multi-tenant administrative company records | `Sops.Models.Tenancy.Chain` | Global Master Contract | Chain UUID, legal business name, tax number, subscription tier, tenant database connection token. |
| **Branch** | Physical store in chain database | `Sops.Models.Tenancy.Branch` | Tenant Branch Entity | Branch code, physical address, phone, license binding, assigned warehouse. |
| **BranchStockSnapshot** | Heavy real-time database links across WAN | `Sops.Models.Chain.BranchStockSnapshot` | Tenant Snapshot (Read-Only) | Projected inventory level. Contains source timestamp and sync revision to prevent stale stock misrepresentation. |
| **TransferRequest** | Ad-hoc phone orders or untracked moves | `Sops.Models.Chain.TransferRequest` | Tenant Coordinated Workflow | Finite state machine: `Draft` -> `Submitted` -> `Approved` -> `Dispatched` -> `Received` -> `Cancelled`. |
| **DeviceFingerprint** | Raw WMI / registry strings exposed in licensing tables | `Sops.Models.Device.DeviceIdentitySummary` | Redacted Summary Contract | Raw hardware signals stay in `Sops.Device`. High-level summary exposes only validated device code and platform. |
| **LicenseCertificate** | Legacy base64 XML blobs | `Sops.Models.Licensing.LicenseClaims` | Universal Claims Contract | Standardized JSON claims: TenantId, BranchId, MaxSeats, Expiration, EnabledModules, SignatureDigest. |
| **RecordChange / Sync** | Custom SQL triggers dumping raw rows | `Sops.Models.Sync.SyncEventEnvelope` | Universal Sync Event Contract | Universal event envelope with correlation ID, aggregate type, mutation kind (`Created`, `Updated`, `Deleted`), and payload. |

---

## 2. Infrastructure vs Domain Model Discard List

The following legacy infrastructure base classes and utility types were audited and deliberately excluded from `Sops.Models`:
- `BaseEntity`, `ArchivableEntity`, `TerminalStaffEntity`: Database ORM plumbing; belongs to data access layers.
- `ModelVerifier`: Local validation reflection utility; replaced by clean domain validation rules and JSON schema validation.
- `DbSet<T>` / `DbContext`: Persistence framework; prohibited in portable contract libraries.
