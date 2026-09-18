# OptiGuard — Brand

Colors as actually implemented in `src/Theme.cs` (three themes, all sharing
the same accent hue so the brand stays recognizable across modes).

## Palette

### Light (default)
| Role | Hex |
|---|---|
| Background | `#F1F5F9` |
| Panel / surface | `#FFFFFF` |
| Panel (secondary) | `#E9EEF6` |
| Header background | `#0F2E27` |
| Header text | `#FFFFFF` |
| Header subtitle | `#8FBFB0` |
| **Accent (primary)** | **`#10B981`** |
| Accent hover | `#059669` |
| Accent light (tint) | `#D1FAE5` |
| Text | `#1E293B` |
| Text muted | `#64748B` |
| Danger | `#EF4444` |
| Danger hover | `#DC2626` |
| Border | `#E2E8F0` |
| Grid alt row | `#F8FAFC` |

### Dark
| Role | Hex |
|---|---|
| Background | `#0F172A` |
| Panel / surface | `#1E293B` |
| Panel (secondary) | `#273349` |
| Header background | `#0B1220` |
| Header text | `#F1F5F9` |
| Header subtitle | `#94A3B8` |
| **Accent (primary)** | **`#10B981`** |
| Accent hover | `#34D399` |
| Accent light (tint) | `#0F3D30` |
| Text | `#E2E8F0` |
| Text muted | `#94A3B8` |
| Danger | `#F87171` |
| Danger hover | `#EF4444` |
| Border | `#334155` |
| Grid alt row | `#19233A` |

### High Contrast (accessibility)
| Role | Hex |
|---|---|
| Background | `#000000` |
| Header background | `#000000` |
| Header text | `#FFFF00` |
| **Accent (primary)** | **`#FFFF00`** |
| Accent hover | `#FFFFFF` |
| Text | `#FFFFFF` |
| Danger | `#FF6B6B` |
| Border | `#FFFFFF` |

The accent is a green (`#10B981` in Light/Dark) — chosen to read as "safe /
verified / go-ahead", matching the product's core promise (evidence-based
cleanup, not fear-mongering). High Contrast swaps to yellow-on-black per
WCAG guidance for maximum-contrast accessibility modes.

## Typography

Currently **Segoe UI** everywhere (`src/Theme.cs`, `src/MainWindow.cs`) — the
Windows system font, guaranteed present on every target OS (Windows 10/11)
with solid built-in Hebrew glyph coverage and native RTL shaping.

**Known gap vs. the cross-tool standard:** the shared standard recommends
Rubik/Heebo/Assistant/Noto Sans Hebrew for stronger Hebrew+English brand
pairing. Segoe UI was kept deliberately for now because switching requires
embedding actual font files (none are bundled in this project yet) and
carries font-license and WPF-embedding risk that wasn't worth taking without
the font files in hand. Revisit if/when brand fonts are sourced for the
whole tool family.

## Icon

`assets/icons/AppIcon.ico` — used for the installer, the main app window, the
taskbar, and Start Menu / desktop shortcuts (all reference the same file via
each `.csproj`'s `ApplicationIcon`). No separate monochrome/dark variant
exists yet — same icon is used across both Light and Dark themes.

## Motion & spacing

No project-local `design-tokens.json` — spacing (4/8/12/16/24/32), radius,
shadow and motion-duration conventions follow the shared cross-tool standard
directly (see `_AUDIT/STANDARDS.md` §3) rather than being duplicated per-tool.
