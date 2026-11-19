# Source-of-Truth Hierarchy & Decision Log

## 1. Hierarchy Rules

When reconciling visual wireframes, HTML design systems, and historical WPF implementations, the following precedence order is strictly enforced:

```
[Visual Identity & Styling]   : HTML Design System & WPF Profiles (Colors, Typography, Radii, Shadows, Brand)
           ↓
[Functional Interaction]      : Historical Sop.Ui & Sop.Design (Keyboard focus, Virtualization, Touch targets, Modals)
           ↓
[Platform & OS Fallbacks]     : Native Windows 7/8.1/10/11 WPF Capabilities (No WebView2, No WinUI3, Software Rendering)
           ↓
[Business Independence]       : Sops.UI.Wpf is 100% Presentational (Zero pharmacy domain or API service dependencies)
```

---

## 2. Visual Identity Hierarchy

1. **Colors**: The palette defined in `Tokens.Light.xaml` and `Tokens.Dark.xaml` faithfully implements the HTML design system:
   - Primary Brand: Teal `#0A6C6E` (Light) / Teal `#0F7C7E` (Dark).
   - Surfaces: Canvas `#F6F8FA` / Surface `#FFFFFF` (Light); Canvas `#0B1114` / Surface `#10181C` (Dark).
   - Typography Ink: Primary `#0F172A` / Muted `#475569` / Subtle `#64748B` (Light); Primary `#F8FAFC` / Muted `#CBD5E1` (Dark).
   - Semantic Status: Success `#2A9D99`, Warning `#DD5B00`, Danger `#DC2626`, Info `#355E9A`.

2. **Typography**:
   - English: `Inter` with fallback to `Segoe UI`, `Tahoma`, `Arial`.
   - Arabic: `IBM Plex Sans Arabic` with fallback to `Segoe UI`, `Tahoma`, `Arial`.
   - Monospace: `JetBrains Mono` with fallback to `Consolas`, `Courier New`.
   - Number Substitution: Hardcoded to `European` digits across all locales to prevent decimal and tabular misalignment on Arabic regional settings.

3. **Touch Targets**:
   - Small: 36px
   - Standard: 44px
   - Large: 56px

---

## 3. Functional Behavior Decisions

1. **Independent Scrolling**:
   - Global scrollbar styles must never encompass the entire window inside a single `ScrollViewer`.
   - Main content, navigation sidebar, tables, cart, and modals scroll independently.
   - Sticky headers (DataTable headers, TopBar, Cart summary action bar) are anchored outside the scrolling viewport.

2. **Keyboard Focus & Modality**:
   - Implemented via `Theme.KeyboardMode`.
   - Mouse clicks do not trigger focus rings; keyboard navigation (`Tab`, arrow keys) activates visible 3px focus rings.
   - Preserves high accessibility without aesthetic clutter during mouse operations.

3. **RTL Mirroring**:
   - Applied to `window.Content.FlowDirection`, never to `Window.FlowDirection`.
   - Avoids Windows OS caption mirroring bugs (`WS_EX_LAYOUTRTL`) on non-Arabic OS builds.
   - Asymmetric glyphs (checkmarks, barcodes, numbers, phone numbers) remain LTR.

4. **Zero Pharmacy Domain Logic**:
   - No drug data, inventory calculation, or barcode decoders are included in `Sops.UI.Wpf`.
   - Components accept primitives, models, data bindings, and standard WPF commands (`ICommand`).
