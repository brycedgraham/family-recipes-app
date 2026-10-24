# TICKET_10 Report — Forest & Earth Palette

Styling-only change in `client/src/app.css` (+ one class addition in `RecipeForm.svelte`). No markup/JS behavior changes; Svelte 5 runes untouched.

## 1. Color tokens (`:root` / `[data-theme="dark"]`)

Work-order names mapped onto the file's actual token names (`--bg` → `--bg-app`, `--surface` → `--bg-surface`, `--surface-hover` → `--hover-bg`, `--border` → `--border-subtle`).

| Token | Light (parchment/birch) | Dark (pine needle/charred timber) |
|---|---|---|
| `--bg-app` | `#f8f5ee` | `#1a221d` |
| `--bg-surface` | `#ede7dc` | `#242e27` |
| `--hover-bg` | `#e2dbce` | `#2c3830` |
| `--primary` | `#2d5a3f` (pine) | `#4e8766` |
| `--primary-hover` | `#224631` | `#407254` |
| `--secondary` | `#8c5835` (terracotta cedar) | `#96613c` (cedar lifted for dark surfaces) |
| `--secondary-hover` | `#754628` | `#7d5130` |
| `--border-subtle` | `#d4cbba` (lichen) | `#3b473f` |
| text set | earth charcoal `#322d24` / `#6a6252` / `#877e6b` | lichen-tinted creams `#e5e9dd` / `#b0b8a6` / `#939d8b` |
| `--danger` | `#a9442e` (kept, already terracotta) | `#d1604a` |

New token families:
- **Wood plank:** `--plank-top/-mid/-bottom/-edge/-text` (`#8a5a34 → #734a2c → #5c3a20`, edge `#4e2f18`, cream text `#fdf6e9`) and a terracotta variant `--plank2-*` for secondary buttons. Dark theme gets deeper "charred timber" stops (`#7e5230 → #684226 → #523318`).
- **Pine bark:** `--bark-base: #33251e`, `--bark-text` (parchment `#ece2cf`), `--bark-text-dim`, `--bark-edge`, and `--bark-texture` — two layered `repeating-linear-gradient`s (fine vertical fissures + wide grain bands) stored in a custom property so sidebar and top nav share one texture definition. No external images, no SVG data-URI needed.

## 2. Wood plank buttons

- Base `button` / `a.button-link` / `a.secondary` now render as milled cedar: vertical `linear-gradient` grain (3 stops), crisp `1px` dark-bronze border (`--plank-edge`), soft bevel (`inset 0 1px 0 rgba(255,233,197,.3)` top highlight + inset bottom shade + 1px drop shadow), cream text.
- `:hover` deepens the grain via `filter: brightness(0.93)`; `:active` depresses per spec: `transform: translateY(1px); box-shadow: inset 0 2px 4px rgba(0,0,0,.3)`.
- `button.secondary` / `a.secondary`: terracotta-cedar variant (`--plank2-*`) so Cancel/Upload/Cook Mode/Print/week-nav buttons read as a second wood tone.
- Reusable `.btn-plank` class added for non-button elements that need the plank look (no current consumer).
- `button.danger` keeps flat red but gained the same bevel + press for tactile consistency.
- **Gradient-leak fixes:** every rule that painted a flat `background-color:` on a button was converted to the `background:` shorthand so it resets the inherited plank gradient: gallery close buttons (`.gallery-x`, `.thumb-x`), gallery thumbs (`.thumb-img`), ingredient Remove buttons, and the danger/secondary variants.
- RecipeForm Cancel button now carries `class="secondary"` (markup) replacing the dead `[type="button"]` color overrides in CSS; the redundant flat-color rules on `.RecipeCard` buttons were deleted (plank styling now inherited from base).
- Small toggle pills (store tabs, scale buttons, pantry/store pills, cook-mode rows, theme toggle, category items) intentionally stay flat — they reset via `background:`/`background: none` and keep their existing active/hover states.

## 3. Pine bark sidebar & navigation

- `.App-sidebar`: dark bark base `#33251e` + shared `--bark-texture` layers over a vertical timber gradient, darker right edge (`--bark-edge`) with inset shadow for depth; text switched to warm parchment (`--bark-text`), including the "Categories" heading and the `<hr>` separator.
- `.CategoryList .category-item`: parchment hover tint; **active state = pine green badge** (`var(--primary)`) with warm parchment text `#f6efdd`, bevel highlight, bold weight (class is `active-category` in this codebase).
- `.App-topnav` (global top nav): same bark treatment (texture + bottom-edge shading); links use dim-parchment → parchment hover; **active link = pine green badge** with parchment text.
- Theme toggle thumb got a 1px drop shadow so it stays visible on the new track colors.

## 4. Contrast notes

- Cream `#fdf6e9` on plank mid `#734a2c` ≈ 6:1 (light) / cream on charred `#684226` ≈ 5.5:1 (dark) — passes AA for normal text, readable at counter distance.
- Active badges/tabs use `var(--primary)` in light (`#2d5a3f`, white/cream ≈ 7–8:1). In dark theme the mandated `--primary: #4e8766` only reaches ~3.9:1 with cream text, so a targeted override deepens all four active-badge states (store tabs, scale buttons, category items, top nav) to `#407254` (white ≈ 5.6:1, cream ≈ 4.9:1 — AA).
- Known trade-off (consequence of the mandated dark `--primary`): small pine-green *text* accents in dark mode (body links, "Added to list", today-heading) sit at ~3.9:1 on `#1a221d`. Flagged as a follow-up candidate if strict AA on all text is required; token values themselves follow the work order exactly.

## 5. Verification

- `npm --prefix client run check` → **189 files, 0 errors, 0 warnings**.
- `npm --prefix client run build` → clean static build, `Wrote site to "dist"`.
- Static inspection: `client/dist/index.html` links `/_app/immutable/assets/0.cMXmm4Sn.css`; bundled CSS contains all new markers (`#2d5a3f`, `#f8f5ee`, `#ede7dc`, `#8c5835`, `#33251e`, `--plank-mid`, `--bark-texture`, `.btn-plank`, dark override `#407254`).
- No headless Chromium launched (per work order); visual rendering not machine-verified.
