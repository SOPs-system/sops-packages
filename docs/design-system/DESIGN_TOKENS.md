# SOPs Design Tokens Reference

## 1. Color System

### Semantic Brushes (Light & Dark Theme Parity)

| Resource Key | Light Hex / Role | Dark Hex / Role | Usage |
|:---|:---:|:---:|:---|
| `Brush.Bg` | `#F6F8FA` | `#0B1114` | Application canvas background |
| `Brush.Surface` | `#FFFFFF` | `#10181C` | Cards, panels, inputs, modals |
| `Brush.SurfaceRaised` | `#FFFFFF` | `#162126` | Elevated cards, menus, popovers |
| `Brush.SurfaceSunken` | `#F0F3F5` | `#0F171A` | Table headers, secondary tags, code blocks |
| `Brush.Overlay` | `#730F171C` (45% opacity) | `#9904080A` (60% opacity) | Modal backdrop dimming |
| `Brush.Line` | `#D7E0E4` | `#1F2A30` | Default card and panel borders |
| `Brush.LineSubtle` | `#E5EBEE` | `#182228` | Table row dividers, inner card dividers |
| `Brush.LineStrong` | `#B5C2C8` | `#2C3A41` | Active input borders, highlighted boundaries |
| `Brush.ScrollbarThumb` | `#C2CED4` | `#2B383F` | Inset scrollbar thumb pill |
| `Brush.ScrollbarThumbHover` | `#93A4AC` | `#445761` | Hovered and dragging scrollbar thumb |
| `Brush.Ink` | `#0F172A` | `#F8FAFC` | Primary text and high-contrast titles |
| `Brush.InkMuted` | `#475569` | `#CBD5E1` | Secondary labels, descriptions, metadata |
| `Brush.InkSubtle` | `#64748B` | `#94A3B8` | Placeholder text, disabled labels |
| `Brush.InkInverse` | `#FFFFFF` | `#0F172A` | Inverted text on solid dark/light buttons |
| `Brush.Primary` | `#0A6C6E` | `#0F7C7E` | Primary action buttons, selected tabs, brand highlights |
| `Brush.PrimaryHover` | `#085658` | `#139496` | Primary button hover |
| `Brush.PrimaryActive` | `#064143` | `#17A9AB` | Primary button pressed state |
| `Brush.PrimarySoft` | `#E4F1F1` | `#0F2A2B` | Soft badges, secondary selected rows |
| `Brush.PrimarySoftHover` | `#B3E4F1F1` | `#B30F2A2B` | Soft button hover |
| `Brush.PrimarySoftBorder`| `#C9E3E3` | `#1A4344` | Soft badge borders |
| `Brush.OnPrimary` | `#FFFFFF` | `#FFFFFF` | Text/icons on primary solid fill |
| `Brush.Success` | `#2A9D99` | `#2A9D99` | Positive financial values, success badges |
| `Brush.SuccessHover` | `#238A86` | `#238A86` | Success action hover |
| `Brush.SuccessSoft` | `#E0F2F1` | `#0F2A29` | Success badges, paid status background |
| `Brush.SuccessSoftText` | `#1E7F7B` | `#3FB3AE` | Text for success soft pills |
| `Brush.Warning` | `#DD5B00` | `#DD5B00` | Low stock, pending sync, expiry warnings |
| `Brush.WarningHover` | `#C05000` | `#C05000` | Warning action hover |
| `Brush.WarningSoft` | `#FCE8DC` | `#2A1A0E` | Warning notification backgrounds |
| `Brush.WarningSoftText` | `#DD5B00` | `#F0812E` | Text for warning soft pills |
| `Brush.Danger` | `#DC2626` | `#DC2626` | Negative balance, expired batch, delete action |
| `Brush.DangerHover` | `#B91C1C` | `#B91C1C` | Danger action hover |
| `Brush.DangerSoft` | `#FCE5E5` | `#2A1010` | Error banners, invalid field backgrounds |
| `Brush.DangerSoftText` | `#DC2626` | `#EF6A6A` | Text for error badges |
| `Brush.Info` | `#355E9A` | `#355E9A` | Informational announcements |
| `Brush.InfoSoft` | `#E2EAF4` | `#14213A` | Info alert backgrounds |
| `Brush.InfoSoftText` | `#355E9A` | `#7BA1D8` | Text for info badges |
| `Brush.FocusRing` | `#400A6C6E` (25% opacity) | `#730F7C7E` (45% opacity) | Accessible focus outline |

---

## 2. Typography Scale

| Token | Size | Line Height | Usage |
|:---|:---:|:---:|:---|
| `Text.2Xs` | 10px | 14px | Micro badges, status indicator dots |
| `Text.Xs` | 12px | 16px | Field hints, metadata, table headers |
| `Text.Sm` | 14px | 20px | Standard body, form inputs, table cells |
| `Text.Md` | 16px | 24px | Button labels, card titles, large inputs |
| `Text.Lg` | 18px | 26px | Section headings, modal titles |
| `Text.Xl` | 22px | 28px | Page titles, primary KPI numbers |
| `Text.2Xl` | 28px | 34px | Register grand total, dashboard headers |
| `Text.3Xl` | 36px | 44px | Large metric displays, login brand text |

---

## 3. Spacing Scale

Based on a strict 4px modular grid:
- `Space.1`: 4px
- `Space.2`: 8px
- `Space.3`: 12px
- `Space.4`: 16px
- `Space.5`: 20px
- `Space.6`: 24px
- `Space.8`: 32px

---

## 4. Corner Radii

- `Radius.None`: 0
- `Radius.Xs`: 5px
- `Radius.Sm`: 6px (Input fields, small buttons, tags)
- `Radius.Md`: 8px (Cards, standard buttons, alerts)
- `Radius.Lg`: 12px (Modals, detail panels, prominent cards)
- `Radius.Xl`: 16px (Large floating sheets)
- `Radius.Full`: 9999 (Pills, circular badges)
- `Radius.Switch`: 11px (Toggle switch track)
- `Radius.ScrollbarThumb`: 3px (Floating scrollbar thumb pill)
