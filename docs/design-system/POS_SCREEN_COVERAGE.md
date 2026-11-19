# 12-Screen POS Inventory Coverage

This document outlines the 12 documented Windows POS screens and maps their layouts and components to `Sops.UI.Wpf`:

| # | Screen Name | Master Layout | Key Reusable Components Used | Functional Preview Capabilities |
|:---:|:---|:---|:---|:---|
| 1 | **Sign In** | `AuthenticationLayout` | Card, TextInput, PasswordInput, Button, Alert, Language/Theme Toggles | Username/password validation preview, shift role selector |
| 2 | **Register / Sale** | `RegisterWorkspaceLayout` | SearchInput, DataTable, QuantityStepper, Button, KpiCard, ModalHost | Barcode search, cart item management, quick keys, grand total footer |
| 3 | **Drug Catalogue** | `ListDetailLayout` | FilterToolbar, DataTable, Badge, DetailPanel, InfoRows, Button | Formulary filtering, batch details, pricing metadata |
| 4 | **Inventory / Stock** | `ListDetailLayout` | FilterToolbar, DataTable, StatusBadge, QuantityStepper, KpiCard | Stock level monitoring, low stock warning chips, expiry indicators |
| 5 | **Purchase Orders / Receiving**| `ListDetailLayout` | DataTable, DetailPanel, StatusBadge, ActionGroup, Modal | PO line items, receiving reconciliation, batch entry |
| 6 | **Suppliers / Warehouses** | `ListDetailLayout` | KpiCard, FilterToolbar, DataTable, DetailPanel, CopyButton | Supplier directory, contact info, balance status, warehouse stock |
| 7 | **Patient Book** | `ListDetailLayout` | SearchInput, DataTable, DetailPanel, InfoRows, Badge | Patient search, chronic medication history, contact chips |
| 8 | **Transactions** | `ListDetailLayout` | DateRangePicker, FilterToolbar, DataTable, DetailPanel, Button | Receipt lookup, payment breakdown, refund initiation modal |
| 9 | **Staff & Shifts** | `ListDetailLayout` | DataTable, StatusChip, Button, DetailPanel, InfoRows | Active shifts, clock-in/out timestamps, role assignments |
| 10 | **Roles & Permissions** | `ListDetailLayout` | DataTable, Checkbox, Badge, DetailPanel, Button | Permission matrix, role capabilities, security level indicators |
| 11 | **Database & Support** | `SettingsLayout` | Card, StatusChip, ProgressBar, Button, InfoRows | Backup status, schema migration progress, local sync health |
| 12 | **Settings** | `SettingsLayout` | Card, Switch, Select, TextInput, Button, Divider | Store details, printer setup, barcode scanner config, tax preferences |
