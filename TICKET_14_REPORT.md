# TICKET_14 Report — Real Wood Plank Texture (`wood_plank.webp`) for Buttons

**Date:** 2026-10-25 · **Status:** DONE — `svelte-check` 0/0, static build clean, deployed via Docker; `/textures/wood_plank.webp` serves `200 image/webp` on the live container.

## 1. What changed

| File | Change |
|---|---|
| `client/static/textures/wood_plank.webp` | New — converted from `wood_plank.jpg` (left untouched) via a 5-line Pillow script: `ImageOps.exif_transpose`, rotate-90-if-vertical (not triggered — source is landscape), `thumbnail((640, 640), LANCZOS)` (no-op — longest edge already 612 px), saved as WEBP quality 82. Result: **612×410 RGB, 44 KB** (vs 45 KB JPEG) |
| `client/src/app.css` | `:root`: added `--plank-photo: url('/textures/wood_plank.webp')`, `--plank-overlay` (warm deep cedar/walnut stain, 3-stop 180° gradient, 0.45→0.68 alpha), `--plank2-overlay` (lighter amber/sienna stain, 0.38→0.52). `[data-theme="dark"]`: deeper charred-timber variants (primary 0.55→0.72, secondary 0.48→0.65). Base `button, a.button-link, a.secondary`, `.btn-plank`, and `button.secondary/a.secondary` now use `background-image: var(--plank-overlay), var(--plank-photo)` (plank2 variant for `.secondary`) with `background-size: cover; background-position: center;` and `text-shadow: 0 1px 2px rgba(0, 0, 0, 0.65)`. Bevel `box-shadow`, `--plank-edge` border, `:hover { filter: brightness(0.93) }`, and `:active { transform: translateY(1px) }` all preserved unchanged |

## 2. How the layering works

Each plank button is two background layers: the real photo (bottom, `cover`/`center` so a horizontal grain band crops across the wide button face) under a translucent stain gradient (top). The solid `background-color: var(--plank-mid)` / `var(--plank2-mid)` stays as fallback. Light mode cream text (`#fdf6e9`) over the 0.45–0.68-alpha cedar/walnut stain keeps ≥5:1 contrast; dark theme's deeper charred overlays do the same for `#f7efdd`.

## 3. Leak audit — small controls stay clean

Every non-plank control resets `background` with a **shorthand** (which also clears `background-image`), so the wood photo cannot leak:

| Control | Reset |
|---|---|
| `.gallery-x`, `.thumb-x` | `background: var(--danger)` |
| `.gallery-thumbs button.thumb-img` | `background: var(--bg-surface)` |
| `button.danger` + `.RecipeForm .ingredient-field button` | `background: var(--danger)` |
| Store filter tabs (`.sl-storebar button`) — incl. the "+ Store" toggle and store "Add" submit, both children of `.sl-storebar` in `ShoppingListDetail.svelte` | `background: var(--bg-surface)` |
| Scale buttons (`.scale-buttons button`) | `background: var(--bg-surface)` |
| Theme toggle | `background: none` (+ hover/focus `background: none`) |
| Category items (`.CategoryList .category-item` — these are `<button>`s, so they'd inherit the base rule) | `background: none` |
| `.sl-store-del` | `background: none` |

## 4. Verification (Docker, live container on :5051)

| Check | Result |
|---|---|
| `npm --prefix client run check` | 189 files, **0 errors / 0 warnings** ✓ |
| `npm --prefix client run build` | adapter-static wrote to `dist`, no errors ✓ |
| `docker compose up -d --build` | image rebuilt, container recreated & started ✓ |
| `curl -I http://localhost:5051/textures/wood_plank.webp` | **200 OK**, `Content-Type: image/webp`, 44,122 bytes ✓ |
| Built CSS (`client/dist/_app/immutable/assets/0.*.css`) | contains both overlay gradients (light + dark) and the `wood_plank.webp` URL ✓ |
