# SOPs Pharmacy Local Server & Management Console — Task Checklist

## Phase 1: Audit & Discovery
- [x] Discover existing packages, tools, and parent instructions
- [x] Inspect existing endpoints in `Sops.DataLayer` and schemas in `Sops.Models`
- [x] Create implementation plan (`tasks/plan.md`) and task breakdown (`tasks/todo.md`)

## Phase 2: Application Host & Entry Point
- [x] Create `apps/Sops.Pharmacy.Server/Sops.Pharmacy.Server.csproj` (Target `net48`, `UseWPF: true`)
- [x] Add `Sops.Pharmacy.Server.csproj` to `SOPs.Packages.sln`
- [x] Implement `Program.Main` routing:
  - [x] `--service`: Run as Windows Service without WPF (Session 0 safe)
  - [x] `--ui`: Run native WPF Management Console using `Sop.Ui`
  - [x] `--console`: Run service in console mode for development/debugging
  - [x] `--health`: Probe loopback health check and return exit code 0 or 1
  - [x] `--install-service` / `--uninstall-service`: Register/unregister Windows service
- [x] Configure `Microsoft.Extensions.DependencyInjection`, logging, and typed configuration

## Phase 3: SQLCipher Persistence Engine
- [x] Configure SQLCipher 4 (`SQLitePCLRaw.bundle_e_sqlcipher` with `Microsoft.Data.Sqlite`)
- [x] Implement `SqlCipherDatabaseProvider` implementing `IRelationalDatabaseProvider`
- [x] Implement database initialization with passphrase, WAL mode, busy timeout, foreign keys
- [x] Create schema migration engine and v1 schema scripts:
  - [x] `local_drugs`, `stock_lots`, `pos_transactions`, `pos_transaction_items`, `pos_shifts`
  - [x] `registered_terminals`, `local_users`, `audit_events`
  - [x] `outbox_events`, `inbox_events`, `sync_checkpoints`, `sync_conflicts`
  - [x] `backup_history`, `schema_migrations`
- [x] Implement integrity verification (`PRAGMA integrity_check`)

## Phase 4: Pharmacy Business Services
- [x] Implement `IDrugCatalogService`: Bilingual search, barcode lookup, normalization via `Sops.Drugs`
- [x] Implement `IInventoryService`: Stock lots, FEFO allocation, stock movements, reservations
- [x] Implement `ISalesService`: Create draft, add lines, add payment, atomic commit, idempotency validation, voids & refunds
- [x] Implement `IShiftService`: Open shift, close shift, discrepancy computation
- [x] Implement `IDeviceService`: Device enrollment, verification, seat limits via `Sops.Device` & `Sops.Licensing`
- [x] Implement `IAuthService`: Password hashing, JWT session tokens, role checks

## Phase 5: RPC & HTTP Server Hosting
- [x] Implement lightweight, robust HTTP/RPC listener hosted in the service
- [x] Route all client endpoints matching `Sops.DataLayer.Endpoints.EndpointRegistry`:
  - [x] `GET /health`, `GET /rpc/info`, `POST /rpc/auth/login`, `logout`, `refresh`, `GET /api/auth/me`
  - [x] `POST /rpc/drugs/searchdrugs`, `getdrugbyid`, `findbybarcode`, `findbysku`
  - [x] `POST /rpc/patients/searchpatients`, `getpatientbyid`, `savepatient`
  - [x] `POST /rpc/inventory/getstocksummary`, `getlotsfordrug`, `adjustlot`
  - [x] `POST /rpc/sales/createsaledraft`, `addsaleline`, `addpayment`, `completetransaction`, `refundtransaction`, `listtransactions`
  - [x] `POST /rpc/shifts/openshift`, `closeshift`, `getexpectedcash`
  - [x] `POST /rpc/categories/listcategories`, `warehouses/listwarehouses`, `settings/getsettings`
  - [x] `POST /rpc/sync/status`, `triggersync`
- [x] Implement administrative management RPC endpoints (`/rpc/management/...`)
- [x] Enforce authentication, rate limiting, and LAN/Loopback permissions

## Phase 6: Data Operations (Sync, Backup, Approvals)
- [x] Implement SQLCipher persistent `IOutboxStore` and `IInboxStore`
- [x] Implement `SqlCipherSyncCheckpointCoordinator`
- [x] Integrate `IEncryptedBackupCoordinator` for scheduled and manual backups
- [x] Integrate `IRestoreCoordinator` for staged restore and rollback
- [x] Integrate `IChangeApprovalCoordinator` for price and stock approvals

## Phase 7: WPF Management Console
- [x] Implement `App.xaml` with design system tokens and merged resources from `Sop.Ui`
- [x] Implement `MainWindow.xaml` with `SopsAppShell`, Sidebar navigation, TopBar, and StatusBar
- [x] Build 11 functional management views:
  - [x] Overview Dashboard
  - [x] Server Control
  - [x] Devices & Terminals
  - [x] Database Management
  - [x] Synchronization
  - [x] Backup & Restore
  - [x] Requests & Approvals
  - [x] Licensing
  - [x] Users & Permissions
  - [x] Logs & Audit Trail
  - [x] Settings (Network, Paths, Sync, Theme, RTL/LTR)
- [x] Implement MVVM ViewModels with real server communication via `Sops.DataLayer`
- [x] Support Light/Dark themes and English/Arabic RTL layouts

## Phase 8: Verification & Automated Tests
- [x] Unit tests for persistence, migrations, business services, and RPC routing
- [x] Integration tests for server startup, shutdown, database encryption, and idempotency
- [x] Verify WPF management console connects, operates, and closes without stopping server
- [x] Full solution build with zero warnings and zero style rule violations
