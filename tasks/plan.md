# SOPs Pharmacy Local Server & WPF Management Console — Master Plan

## 1. Executive Summary & Goals
The SOPs Pharmacy Local Server is the authoritative on-premises server for a pharmacy running on Windows (Windows 7 SP1 x86/x64 through Windows 11).
It consists of:
1. **Windows Service Mode (`--service`)**: A non-GUI, Session-0-safe runtime providing local encrypted storage, background synchronization, automated backups, and hosting local RPC/HTTP endpoints for POS terminals and the management console.
2. **WPF Management Console Mode (`--ui`)**: A native C#/XAML administrative interface built with `Sop.Ui` that connects to the running service via an authenticated loopback management channel. Closing the management window never stops the server.
3. **CLI Utilities**: `--console` (interactive development runtime), `--health` (operational health probe), `--install-service`, `--uninstall-service`.

## 2. Package Reuse & Extension Matrix

| Package | Role in Architecture | Integration / Extension Strategy |
| :--- | :--- | :--- |
| `Sop.Ui` | Reusable WPF Design System | Reused for all UI components, tokens, themes, AppShell, Sidebar, StatusBar, and our 14 controls (NumericInput, DateInput, BarcodeInput, AsyncLookup, EmptyState, NumericKeypad, TableToolbar, ShortcutOverlay, SyncStatus, SopsStepper, Breadcrumbs, FilterChips, SplitButton, CalendarRangePicker). |
| `Sops.Models` | Domain models & contracts | Reused for Drug, StockLot, LocalDrug, Transaction, Shift, Sync contracts, Licensing. |
| `Sops.Drugs` | Bilingual search & drug normalization | Reused for barcode validation, drug name normalization, and bilingual catalog search indexing. |
| `Sops.Database` | Relational database abstraction | Reused for `IRelationalDatabaseProvider`, transactions, and retry execution. Extended with SQLCipher provider integration. |
| `Sops.DataLayer` | Client transport & RPC dispatch | Reused for RPC and HTTP routing conventions, endpoint definitions, session management, and `ISopsDataLayer` client facade. |
| `Sops.DataOperations` | Offline sync, backup, approvals | Reused for `IEncryptedBackupCoordinator`, `IRestoreCoordinator`, `IChangeApprovalCoordinator`, `ISyncCoordinator`. Extended with SQLCipher persistent outbox and inbox stores. |
| `Sops.Cache` | Memory cache | In-memory cache for catalog and stock summaries with cache-aside pattern. |
| `Sops.Configuration` | Configuration and settings | Reused for validated typed server options with secret redaction. |
| `Sops.Device` | Device identity | Reused for hardware fingerprinting and terminal identity verification. |
| `Sops.Licensing` | License validation | Reused for license verification and terminal seat entitlement. |
| `Sops.Logging` | Structured logging | Reused for log retention and structured operational diagnostics. |

## 3. Database & SQLCipher Architecture
Authoritative local operational storage uses SQLCipher 4 (`SQLitePCLRaw.bundle_e_sqlcipher` with `Microsoft.Data.Sqlite`).
Schema tables:
- `local_drugs`: Local pricing, packaging, min/max thresholds.
- `stock_lots`: Batch numbers, expiry dates, quantities, FEFO order.
- `pos_transactions` & `pos_transaction_items`: Completed and draft sales, atomic writes, idempotency keys.
- `pos_shifts`: Cashier shifts, opening/closing floats, discrepancy calculations.
- `registered_terminals`: Device authorization, seat assignments, last seen timestamps.
- `local_users`: Cashiers and administrators, hashed credentials, permissions.
- `audit_events`: Tamper-evident audit trail of administrative actions.
- `outbox_events` & `inbox_events`: Persistent offline-first synchronization queues.
- `sync_checkpoints` & `sync_conflicts`: Durable checkpoint records and conflict isolation.
- `schema_migrations`: Version tracking and atomic migration records.
- `backup_history`: Verified encrypted backup manifests.

## 4. Networking & RPC Endpoints
Local server hosts an authenticated HTTP/RPC server (default port 5040, LAN configurable):
- Public/Unauthenticated: `GET /health`, `GET /rpc/info`, `POST /rpc/auth/login`.
- Authenticated POS terminal endpoints:
  - Drugs: `POST /rpc/drugs/searchdrugs`, `getdrugbyid`, `findbybarcode`, `findbysku`.
  - Inventory: `POST /rpc/inventory/getstocksummary`, `getlotsfordrug`, `adjustlot`.
  - Sales: `POST /rpc/sales/createsaledraft`, `addsaleline`, `addpayment`, `completetransaction`, `refundtransaction`, `listtransactions`.
  - Shifts: `POST /rpc/shifts/openshift`, `closeshift`, `getexpectedcash`.
  - Patients & Categories & Warehouses: `patients.*`, `categories.*`, `warehouses.*`.
- Authenticated Management endpoints (Loopback/Admin):
  - `/rpc/management/status`, `devices`, `database`, `backup`, `restore`, `sync`, `approvals`, `logs`, `control`.

## 5. WPF Management Console Modules
The native WPF application (`--ui`) hosts an MVVM console using `Sop.Ui`:
1. **Overview Dashboard**: Live uptime, server status, terminals, database size, sync state, CPU/memory, alert banner.
2. **Server Control**: Listener state, port configuration, service start/stop/restart, health checks.
3. **Devices & Connections**: Registered terminals, approval/revocation, license seat allocation.
4. **Database Management**: Encryption verification, schema version, integrity check, vacuum/maintenance.
5. **Synchronization**: Cloud connection status, pending outbox/inbox queues, conflict list, trigger sync.
6. **Backup & Restore**: Backup history, manual backup action, restore preview, staged safe restore.
7. **Requests & Approvals**: Pending price/stock change requests, approve/reject workflow.
8. **Licensing**: Active license details, pharmacy assignment, validity period, seat limits.
9. **Users & Permissions**: Local users, roles (Admin, Pharmacist, Cashier), active cashier shifts.
10. **Logs & Audit**: Searchable structured log entries, audit trail.
11. **Settings**: Network bindings, database path, backup destination, sync intervals, theme & Arabic/English toggle.

## 6. Implementation Slices
- **Slice 1**: Application project scaffold & dual entry point (`--service`, `--ui`, `--console`, `--health`).
- **Slice 2**: SQLCipher persistence engine, schema migrations, and repositories.
- **Slice 3**: Core pharmacy business services (Drug indexing, Inventory, Sales, Shifts, Devices).
- **Slice 4**: Local RPC & HTTP Server hosting (routing to `EndpointRegistry` routes and management routes).
- **Slice 5**: Data operations integration (Persistent SQLCipher Outbox/Inbox, Backup & Restore, Approvals).
- **Slice 6**: Native WPF Management Console views and viewmodels with `Sop.Ui`.
- **Slice 7**: Automated test suite, integration tests, and deployment verification.
