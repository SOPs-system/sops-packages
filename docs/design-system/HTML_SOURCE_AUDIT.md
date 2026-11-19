# HTML Source Audit & Component Classification Matrix

## 1. Executive Summary

This document presents the complete audit and classification of the 54 design-system component patterns identified across the SOPs design-system profiles:
1. **Sop.Ui (WPF)** — Native Windows desktop components optimized for cashier operations, touch targets, and high-speed keyboard input.
2. **Sop.Design (WPF)** — Desktop design system tokens, typography scales, advanced controls, and gallery preview views.
3. **SOP Platform** — Reference platform architecture, visual brand identity, and color token definitions.
4. **Pharmacist Mobile** — Mobile responsive patterns (audited to adapt generic interaction principles while maintaining desktop ergonomics).

---

## 2. Component Classification & Coverage Matrix

Every catalogued pattern is classified into one of six standard architectural tiers:
- **P**: Reusable WPF Primitive (self-contained atomic control)
- **C**: Reusable WPF Composite (compound control combining primitives)
- **L**: Generic WPF Layout (reusable structural layout template)
- **A**: POS-Specific Application Component (screen-specific compound control)
- **W**: Web/Mobile-Only Pattern (adapted or excluded from desktop package)
- **D**: Documentation / Reference Item

| # | Component Pattern | Tier | Historical Source Equivalent | New `Sops.UI.Wpf` Component | Status | Visual Tokens & Dimension Targets |
|:---|:---|:---:|:---|:---|:---:|:---|
| 1 | **Button** | P | `Sop.Ui.Controls.Button` / `SopButton` | `Sops.UI.Wpf.Controls.Button` | Implemented | Height: 36px (Sm), 44px (Md), 56px (Lg); Variants: Primary, Secondary, Soft, Ghost, Success, Warning, Danger; States: Default, Hover, Pressed, Focused, Disabled, Loading |
| 2 | **IconButton** | P | `Sop.Ui.Controls.IconButton` | `Sops.UI.Wpf.Controls.IconButton` | Implemented | Square aspect ratio (36/44/56px); transparent/hoverable; supports badge overlay |
| 3 | **SplitButton / ActionGroup** | C | `Sop.Design.Controls.Buttons` | `Sops.UI.Wpf.Controls.ActionGroup` | Implemented | Segmented button group with single-side corner radii and unified focus chain |
| 4 | **CopyButton** | P | `Sop.Features.Common.CopyButton` | `Sops.UI.Wpf.Controls.CopyButton` | Implemented | Visual feedback on click (swaps icon to checkmark for 1.5s) |
| 5 | **ShortcutHint / Kbd** | P | `Sop.Ui.Controls.Kbd` / `HotKey` | `Sops.UI.Wpf.Controls.Kbd` | Implemented | Keyboard key badge `[F1]`, `[Ctrl+K]`, subtle border, mono font |
| 6 | **TextInput** | P | `Sop.Ui.Controls.Input` | `Sops.UI.Wpf.Controls.TextInput` | Implemented | Height: 44px; Prefix/Suffix slots; Clear button; Focused ring |
| 7 | **PasswordInput** | P | `Sop.Ui.Controls.PasswordInput` | `Sops.UI.Wpf.Controls.PasswordInput` | Implemented | Masked bullet input with toggleable show/hide eye icon |
| 8 | **NumberInput / NumericBox** | P | `Sop.Design.Controls.SopNumericBox` | `Sops.UI.Wpf.Controls.NumericBox` | Implemented | Numeric validation, Min/Max clamps, Step buttons |
| 9 | **DecimalInput** | P | `SopNumericBox` (Decimal mode) | `Sops.UI.Wpf.Controls.NumericBox` | Implemented | Culture-aware decimal separator support, European digit pinning |
| 10 | **CurrencyInput / MoneyInput** | P | `Size.MoneyInput` (112px) | `Sops.UI.Wpf.Controls.CurrencyInput` | Implemented | Tabular western figures, currency symbol affix, financial rounding |
| 11 | **SearchInput** | C | `Sop.Design.Controls.SopSearchBox` | `Sops.UI.Wpf.Controls.SearchInput` | Implemented | Search icon, shortcut hint, instant search debounce, clear action |
| 12 | **Select / ComboBox** | P | `Sop.Ui.Controls.Select` | `Sops.UI.Wpf.Controls.Select` | Implemented | Themed popup dropdown, search filtering, virtualized list |
| 13 | **DateInput** | C | `Sop.Design.Controls.DateInput` | `Sops.UI.Wpf.Controls.DateInput` | Implemented | Text entry with ISO/locale parser and popup calendar picker |
| 14 | **DateRangeInput** | C | `Sop.Features.Common.DateRangeModal` | `Sops.UI.Wpf.Controls.DateRangePicker` | Implemented | Quick presets (Today, This Week, This Month) + dual calendars |
| 15 | **Field** | C | `Sop.Ui.Controls.Field` | `Sops.UI.Wpf.Controls.Field` | Implemented | Unified form field wrapper: Label, Hint, Required indicator, Error |
| 16 | **FieldLabel** | P | `Sop.Ui.Controls.Field.Label` | `Sops.UI.Wpf.Controls.FieldLabel` | Implemented | Semantic typography, accessibility automation name association |
| 17 | **FieldHint** | P | `Sop.Ui.Controls.Field.Hint` | `Sops.UI.Wpf.Controls.FieldHint` | Implemented | Subtle secondary text beneath inputs for guidance |
| 18 | **FieldError** | P | `Sop.Ui.Controls.Field.Error` | `Sops.UI.Wpf.Controls.FieldError` | Implemented | Danger color, error icon, accessible live region announcement |
| 19 | **Checkbox** | P | `Sop.Ui.Controls.Checkbox` | `Sops.UI.Wpf.Controls.Checkbox` | Implemented | 20x20px touch target; checked/unchecked/indeterminate; keyboard Space |
| 20 | **RadioButton** | P | `Sop.Ui.Controls.Radio` | `Sops.UI.Wpf.Controls.RadioButton` | Implemented | Circular selection dot; keyboard arrow navigation in group |
| 21 | **Switch** | P | `Sop.Ui.Controls.Switch` | `Sops.UI.Wpf.Controls.Switch` | Implemented | 38x22px pill track, 16px circular thumb, smooth translation |
| 22 | **QuantityStepper** | C | `Size.RefundQty` / `Size.StepButton` | `Sops.UI.Wpf.Controls.QuantityStepper`| Implemented | Direct numeric entry flanked by `[-]` and `[+]` 32px hit targets |
| 23 | **TagsInput** | C | `Sop.Design.Controls.TagsInput` | `Sops.UI.Wpf.Controls.TagsInput` | Implemented | Multi-chip collection entry with inline chip removal |
| 24 | **NavigationRail** | C | `Size.SidebarRail` (64px) | `Sops.UI.Wpf.Controls.NavigationRail` | Implemented | Vertical icon navigation rail with tooltips and active indicator |
| 25 | **CollapsibleSidebar** | C | `Sop.App.Shell.Sidebar` (224px) | `Sops.UI.Wpf.Controls.CollapsibleSidebar` | Implemented | Width toggle between 224px and 64px, grouped sections, badge counters |
| 26 | **NavigationItem** | P | `Sidebar.NavItem` | `Sops.UI.Wpf.Controls.NavigationItem` | Implemented | Icon + Label + Badge + Active left/right accent border |
| 27 | **Tabs** | C | `Sop.Ui.Controls.Tabs` | `Sops.UI.Wpf.Controls.Tabs` | Implemented | Pill and underline variants, keyboard Left/Right arrow navigation |
| 28 | **SegmentedControl** | C | `Sop.Ui.Controls.SegmentedControl` | `Sops.UI.Wpf.Controls.SegmentedControl` | Implemented | Compact multi-option toggle container with pill active highlight |
| 29 | **Breadcrumb** | C | `Sop.Design.Gallery.Navigation` | `Sops.UI.Wpf.Controls.Breadcrumb` | Implemented | Chevron separator, navigable parent links, current page item |
| 30 | **TopBar** | C | `Sop.App.Shell.TopBar` (56px) | `Sops.UI.Wpf.Controls.TopBar` | Implemented | Window branding, search slot, shift/operator status, theme/lang actions |
| 31 | **StatusBar** | C | `Sop.App.Shell.StatusBar` (32px) | `Sops.UI.Wpf.Controls.StatusBar` | Implemented | Network status, sync queue count, terminal ID, keyboard shortcut reminder |
| 32 | **Pagination / Pager**| C | `Sop.Design.Controls.Pager` | `Sops.UI.Wpf.Controls.Pager` | Implemented | Page size selector, Previous/Next, page range display |
| 33 | **ContextMenu** | C | `Sop.Ui.Controls.Menu` / `AppContextMenu` | `Sops.UI.Wpf.Controls.ContextMenu` | Implemented | Themed popup menu with icon column, shortcut column, separator |
| 34 | **DropdownMenu** | C | `Sop.Ui.Controls.Menu` | `Sops.UI.Wpf.Controls.DropdownMenu` | Implemented | Trigger button attached popup menu |
| 35 | **DataTable / Table** | C | `Sop.Ui.Controls.Table` / `SopTable` | `Sops.UI.Wpf.Controls.DataTable` | Implemented | Full virtualization (`VirtualizingStackPanel`), sticky header, alternating rows |
| 36 | **DataTableColumn** | P | `Sop.Ui.Controls.Table.Column` | `Sops.UI.Wpf.Controls.DataTableColumn`| Implemented | Column sort glyph, alignment, numeric formatting, resizable grippers |
| 37 | **FilterControl** | C | `Size.FilterBox` (256px) | `Sops.UI.Wpf.Controls.FilterToolbar` | Implemented | Search query + status filters + date range filter toolbar |
| 38 | **KpiCard / MetricCard** | C | `Sop.Features.Common.KpiMin` (160px) | `Sops.UI.Wpf.Controls.KpiCard` | Implemented | Metric value, delta indicator (% increase/decrease), icon, sparkline slot |
| 39 | **DetailPanel** | C | `Size.DetailPanel` (280-720px) | `Sops.UI.Wpf.Controls.DetailPanel` | Implemented | Resizable split boundary with drag grip, header, scrollable body, actions |
| 40 | **InfoRows / DetailList** | C | `Sop.Features.Common.DetailList` | `Sops.UI.Wpf.Controls.InfoRows` | Implemented | Clean aligned 2-column key-value list with copyable identifiers |
| 41 | **ListCard** | C | `Sop.Features.Common.ListCard` | `Sops.UI.Wpf.Controls.ListCard` | Implemented | Summary card with title, badge, metadata line, action buttons |
| 42 | **EmptyState** | C | `Sop.Design.Gallery.Components` | `Sops.UI.Wpf.Controls.EmptyState` | Implemented | Graphic icon, bold title, descriptive copy, primary CTA button |
| 43 | **SkeletonRow / Pulse** | P | `Sop.Ui.Controls.Skeleton` | `Sops.UI.Wpf.Controls.Skeleton` | Implemented | Shimmering placeholder pulse animation for text, circles, rectangles |
| 44 | **StatusBadge / Badge** | P | `Sop.Ui.Controls.Badge` / `SopBadge` | `Sops.UI.Wpf.Controls.Badge` | Implemented | Soft background + strong text pill: Neutral, Success, Warning, Danger, Info |
| 45 | **Alert** | C | `Sop.Ui.Controls.Alert` | `Sops.UI.Wpf.Controls.Alert` | Implemented | Contextual banner: Icon, Title, Message, Dismiss action |
| 46 | **Toast** | C | `Sop.Ui.Controls.Toast` / `SopToast` | `Sops.UI.Wpf.Controls.Toast` | Implemented | Transient notification card (320px) positioned bottom-center/bottom-right |
| 47 | **Banner** | C | `Sop.Features.Common.Banner` | `Sops.UI.Wpf.Controls.Banner` | Implemented | Top-of-screen persistent notification strip |
| 48 | **StatusChip** | P | `SopStatusIndicator` | `Sops.UI.Wpf.Controls.StatusChip` | Implemented | 8px status indicator dot + label (Online, Offline, Syncing, Error) |
| 49 | **LoadingSpinner** | P | `Sop.Ui.Controls.Spinner` | `Sops.UI.Wpf.Controls.Spinner` | Implemented | Continuous rotating SVG arc (Sm: 14px, Md: 18px, Lg: 24px) |
| 50 | **ProgressBar** | P | `Sop.Design.Controls.Progress` | `Sops.UI.Wpf.Controls.ProgressBar` | Implemented | Determinate and indeterminate horizontal progress bar (8px track) |
| 51 | **ProgressRing** | P | `Sop.Design.Controls.ProgressRing` | `Sops.UI.Wpf.Controls.ProgressRing` | Implemented | Circular radial percentage progress ring |
| 52 | **StepProgress** | C | `Sop.Design.Gallery.Foundations` | `Sops.UI.Wpf.Controls.StepProgress` | Implemented | Multi-step transaction stepper (Numbered dots, connection line, labels) |
| 53 | **Card / Surface** | P | `Sop.Ui.Controls.Card` / `Surface` | `Sops.UI.Wpf.Controls.Card` | Implemented | Base container with token-driven radius, border, and elevation |
| 54 | **Modal / ModalHost** | C | `Sop.Ui.Controls.Modal` / `ModalHost`| `Sops.UI.Wpf.Controls.Modal` | Implemented | Full backdrop, focus trap, Escape to close, Sm (384) / Md (512) / Lg (768) |

---

## 3. Generic Layout Templates

The 5 generic layout templates implemented in `Sops.UI.Wpf.Layouts`:
1. **`AppShell`** — Master application chrome: TopBar, CollapsibleSidebar/Rail, Content Host, StatusBar, and ModalHost.
2. **`RegisterWorkspaceLayout`** — Dual-panel cashier workspace: Search header, Item selection grid, Cart panel with resizable splitter, and sticky checkout footer.
3. **`ListDetailLayout`** — Master-detail workspace: KPI header summary, Filter toolbar, Virtualized table, Pagination, and resizable DetailPanel.
4. **`AuthenticationLayout`** — Split-screen authentication: Left brand panel with typography & logo, Right login form with language/theme switchers.
5. **`SettingsLayout`** — Grouped settings workspace: Scrollable settings cards, section labels, switches, dropdowns, and input fields.
