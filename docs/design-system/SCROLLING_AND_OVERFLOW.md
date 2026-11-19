# Theming & Runtime Theme Switching

## 1. Principles

1. **Zero Control-Level Hardcoded Colors**: Controls never hardcode hex values. All templates reference `DynamicResource` tokens (e.g. `{DynamicResource Brush.Surface}`, `{DynamicResource Brush.Ink}`).
2. **Instant Runtime Switching**: Theme swapping is achieved by replacing the token dictionary in `Application.Current.Resources.MergedDictionaries`. No window restarts or reloads required.
3. **Themes Supported**:
   - `ThemeMode.Light` (Standard pharmacy daytime operations)
   - `ThemeMode.Dark` (Low-light and night shifts)
   - `ThemeMode.HighContrast` (Accessibility compliance for low-vision operators)

---

# RTL & Bilingual Localization (English & Arabic)

## 1. Principles

1. **Direction Mirroring**:
   - Layout direction is attached to the content root (`window.Content.FlowDirection = Theme.FlowDirection`).
   - The OS window chrome and title bar remain untouched to prevent `WS_EX_LAYOUTRTL` Windows rendering artifacts.
2. **Font Auto-Switching**:
   - When `Theme.IsRtl = true`, `Font.App` points to `Font.Ar` (`IBM Plex Sans Arabic`, fallback `Segoe UI`, `Tahoma`).
   - When `Theme.IsRtl = false`, `Font.App` points to `Font.En` (`Inter`, fallback `Segoe UI`, `Arial`).
3. **Non-Mirrored Content**:
   - Numbers and prices are strictly preserved with `NumberSubstitutionMethod.European`.
   - Barcodes, phone numbers, transaction hashes, and left-aligned directional icons are pinned to `FlowDirection.LeftToRight`.

---

# Scrolling and Overflow System

## 1. Global Scrollbar Design

- Inset floating pill: 12px gutter width, transparent track, 6px wide rounded pill thumb with 3px margins.
- Hover & Dragging visual states via dynamic brushes `Brush.ScrollbarThumb` and `Brush.ScrollbarThumbHover`.

## 2. Independent Scrolling Architecture

1. **TopBar & StatusBar**: Fixed outside the scrolling viewport.
2. **Sidebar**: Independently scrollable if navigation items exceed screen height.
3. **Main Content**: Bounded scrolling region with virtualization enabled for lists and tables.
4. **Data Tables**: Table headers (`DataGridColumnHeader`) remain pinned at the top while rows scroll vertically. Horizontal scrolling engages only when table columns exceed container width.
5. **POS Cart**: Right-hand checkout items scroll vertically independently while grand totals and payment buttons stay sticky at the bottom.
6. **Modals**: Modal header and footer action buttons are pinned; modal body scrolls when content overflows.
