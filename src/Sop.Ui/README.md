# `Sop.Ui` — the design system

Everything visual in the app comes from here; nothing in `Sop.Features` or
`Sop.App` may define a colour, a font or a size. `Sop.Ui` references
`Sop.Core` only — the primitives are presentational and know nothing about
services, screens or the shell.

## Files

| | |
| --- | --- |
| `Themes/Tokens.Base.xaml` | Faces (`Font.App`, `Font.Mono`, embedded from `Fonts/`), the type ramp (`Text.Xs…`), radii (`Radius.*`), sizes (`Size.*` — hit targets, menu and modal widths, the cart column), paddings (`Pad.*`), spacing (`Space.*`), motion (`Motion.*`). |
| `Themes/Tokens.Light.xaml` / `Tokens.Dark.xaml` | Colours (`Color.*`), brushes (`Brush.*`), shadows (`Shadow.*`), focus rings (`Ring.*`). **Identical key sets** — `TokenTests` proves it — swapped at runtime by `Theme`. |
| `Themes/Icons.xaml` | Stroke-icon geometries, `Icon.<Glyph>`, authored on a 24×24 box. |
| `Themes/Scrollbars.xaml` | Themed scrollbars, app-wide. |
| `Themes/Generic.xaml` | Merges `Controls/*.xaml` — the default style of every primitive. |
| `Controls/<Name>.cs` + `.xaml` | One primitive per pair (below). |
| `Theme.cs` | `Theme.Initialize(app)`, `Theme.Mode`, `Theme.IsRtl`, `Theme.Attach(window)`, `Theme.KeyboardMode` (the `:focus-visible` equivalent). |
| `Enums.cs` | `ControlSize` (Sm 36 · Md 44 · Lg 56 — the POS touch targets), `ButtonVariant`, `BadgeVariant`, `ToastVariant`, `StatusVariant`, `ModalSize`, `MenuAlign`, `ThemeMode`. |

## Primitives

| Control | What it is |
| --- | --- |
| `Button`, `IconButton` | The one button; variants live in `Button.xaml` and nowhere else. `Loading` disables and spins. |
| `Input`, `PasswordInput`, `Select` | The shared control frame — a text box and a picker are visually identical in a form row. `Select` is a combobox: the field filters as you type, ten options at a time in both directions, a "show more/earlier" row for lists too short to scroll. |
| `Field` | Label, hint and error around a control; an `Error` turns the control red by itself. |
| `Checkbox`, `Switch`, `Tabs` | The toggles and the segmented strip. |
| `Card`, `Divider`, `Badge`, `Alert`, `Kbd`, `Spinner`, `Icon` | Surfaces and small parts. `Card.Interactive` gives a tile hover, focus and `Click`. |
| `Table` | A `DataGrid` locked to the register's needs: sticky header, drag-to-resize columns (only a dragged width is remembered, per `ColumnsKey`), single selection that scrolls itself into view, pixel-scrolled and virtualised. |
| `Menu` + `MenuItem` | The one dropdown popover — Select and the sort menus are both built on it. `Menu.Prewarm` is called once at startup so the first dropdown of a session does not pay for the popup machinery. |
| `Modal` + `ModalHost` | The overlay layer. `<ui:ModalHost/>` is the last child of a window's root Grid; a `ui:Modal` declared anywhere presents there. |
| `Toast` | The notice with a close button. |
| `AppWindow` | A window wearing the register's own chrome (mark, title, the three caption buttons) — but only where the desktop is composited: Windows 7 without Aero draws its frame over ours, so there the SYSTEM caption stays and the template hides its own bar (`UsesOwnChrome`). Never opens larger than the work area, leaves the real system frame metric around a maximised window, and releases idle memory to Windows when minimised. |
| `PopupGuard` | Attached behaviour: a popup that follows its window (closes on deactivate, move, resize). |

## Rules

1. **Add a token, not a value.** Raw hex, `Brushes.*`, a literal `FontSize`
   or `FontFamily` anywhere outside `Themes/Tokens.*.xaml` fails
   `StyleRulesTests`. Layout numbers on the 4px grid (`Margin="0,0,12,12"`)
   are fine, the way Tailwind's `gap-3` is.
2. **Everything visual is a `DynamicResource`** so `Theme.Mode` re-themes a
   running window with no code. A brush resolved once — through a converter,
   a `TemplateBinding`, code — freezes at the theme it was resolved in.
3. **Variants live in the primitive's XAML**, never at the call site.
4. **RTL by construction.** WPF mirrors layout under `FlowDirection`; use
   `Left`/`Right` to mean start/end, and pin asymmetric glyphs (the check
   mark) to `FlowDirection="LeftToRight"`.
5. **`Radius.Full` is for square boxes only.** WPF scales an oversized radius
   to width/2 *and* height/2, so a wide pill becomes an ellipse; a pill takes
   its radius from its own height (`HalfHeightToCornerRadiusConverter`).
6. **A plain `ItemsControl` sizes its rows to their content.** Give it a
   stretching `ItemContainerStyle` or the highlight stops short of the edge.
7. **A `Popup` is its own top-level window.** Close it on the owner's
   deactivate (`PopupGuard`), and never leave one open at the end of a test —
   it swallows the next test's keystrokes.
8. **Focus rings are keyboard-only** (`Theme.KeyboardMode`).
9. **Fonts are embedded** (`Fonts/*.ttf` as `Resource`), so nothing depends
   on what Windows ships — Windows 7 included.

## Motion

The web's `transition-colors` is reproduced with overlay layers whose opacity
animates (WPF cannot colour-animate a `DynamicResource` brush): hover and
pressed tints, focus frames, the Switch thumb, the Tabs segment, the Select
chevron, all over 150 ms. Menus fade and settle in over 100 ms; Modal and
Toast keep the theme's entrance, played from code (a Loaded storyboard in a
template resolves against the wrong name scope inside a UserControl).

## Known deviations from the web design system

- Table row hover is immediate (DataGrid rows share one brush).
- No `letter-spacing` on table headers and menu captions.
- Shadows are `DropShadowEffect`s — on cards, menus, modals and toasts only.
- No colour emoji (they do not render in WPF on .NET Framework).

## Checking it

`Sop.Gallery` (`dotnet run --project src/Sop.Gallery`) shows every primitive
in every variant with dark and RTL toggles; a primitive is done when it reads
correctly in all three. `tests/Sop.Ui.Tests` runs the style rules, token
parity, live control tests and the gallery's visual baselines
(`Visual/baseline/*.png`, approved with `scripts/visual.ps1 -Update`).
