# TICKET_3 Report — Culinary Fraction Formatter + React → SvelteKit (Svelte 5) Cutover

**Date:** 2026-10-23 · **Status:** DONE — backend build clean, `svelte-check` 0 errors / 0 warnings, static SPA build green, 43/43 browser E2E assertions passing (two consecutive runs).

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp.Core/IngredientText.cs` | New `FormatNumber(decimal)` — decimals within 0.01 of a standard culinary fraction render as ASCII fractions; near-integers snap to the integer; everything else keeps ≤4 decimals with trailing zeros trimmed. `FormatQuantity` now delegates to it (`"0.6667 cup"` → `"2/3 cup"`) |
| `client/` | **Replaced**: React 19 + react-router app → SvelteKit (Svelte 5) SPA, `@sveltejs/adapter-static` with `fallback: 'index.html'`, `export const ssr = false;` in `src/routes/+layout.js`. Vite proxy `/api` → `http://localhost:5051` preserved |
| `client/src/lib/api.js` | Preserved `client/src/api.js` moved verbatim (fetch layer, all endpoints unchanged); imported via the `#lib` alias |
| `client/src/lib/format.js` | New — JS equivalent of `IngredientText.FormatNumber`: `formatNumber(value)` + `formatQuantity(amount, unit)`, used for client-side 0.5x/1x/2x/3x scaling and shopping-list quantity display |
| `client/src/app.css` | Preserved `App.css` (all styles reused); only the `.CategoryList li` selectors retargeted to `.CategoryList .category-item` buttons (a11y fix, §4) |
| `client/src/routes/…` | All views ported (§3) — layout/nav/theme, home browser, recipe new/edit/detail, shopping-lists overview + detail + shared read-only route |
| `AGENTS.md` | Frontend stack bullet updated; new **"Svelte 5 Runes Rules (Strict)"** section (required vs banned syntax table + conventions); client commands now include `npm --prefix client run check` |

## 2. Fraction formatting

Both sides share the same rule set — fractions within **0.01**: `1/8, 1/4, 1/3, 3/8, 1/2, 5/8, 2/3, 3/4, 7/8`:

| Input (decimal) | Output |
|---|---|
| `0.6666` | `2/3` |
| `1.3333` | `1 1/3` |
| `0.5` / `0.25` | `1/2` / `1/4` |
| `0.99` / `2.004` | `1` / `2` (near-integer snap) |
| `2.5` / `4.0` | `2.5` / `4` (trailing zeros trimmed) |

**Verified via curl** against the running API (`/api/recipes`, through the Vite proxy): all 76 recipes, **172 ingredient display strings now carry ASCII fractions** (`Alfredo: 1/2 cup`, `Apple Butter Pancakes: 2/3 cup`, `Banana Pancakes: 1 1/2 cup`), **0 raw long-decimals remaining**. Note: the API process had to be restarted after the rebuild — a stale binary kept serving `"0.6667 cup"` until then, which the curl check caught.

## 3. SvelteKit SPA structure

```
client/
├── vite.config.js                  # /api proxy → http://localhost:5051 (unchanged behavior)
├── svelte.config.js                # adapter-static, fallback: 'index.html'
└── src/
    ├── app.html · app.d.ts · app.css   # preserved CSS imported in +layout.svelte
    ├── routes/
    │   ├── +layout.js              # export const ssr = false;
    │   ├── +layout.svelte          # sticky top nav (Recipes, Shopping Lists) + ThemeToggle, {@render children()}
    │   ├── +page.svelte            # home: CategoryList sidebar, debounced ?q= search (300ms), RecipeCard grid, Add link
    │   ├── recipes/new/+page.svelte        # shared RecipeForm (create mode)
    │   ├── recipes/[id]/+page.svelte       # detail: 0.5x/1x/2x/3x scaling via format.js, Add to Shopping List, Edit, Delete
    │   ├── recipes/[id]/edit/+page.svelte  # shared RecipeForm (edit mode, prefilled)
    │   ├── shopping-lists/+page.svelte     # overview: counts, New List, Delete
    │   ├── shopping-lists/[id]/+page.svelte     # full detail (writable)
    │   └── shopping-lists/shared/[token]/+page.svelte  # read-only shared view
    └── lib/
        ├── api.js                  # verbatim from React client
        ├── format.js               # fraction-aware number/quantity formatting
        └── components/             # CategoryList, RecipeCard, RecipeDetail, RecipeForm, ShoppingListDetail, ThemeToggle
```

Feature parity with the React app: category filter + debounced search, recipe CRUD with dynamic ingredient rows/steps/categories, quantity scaling with friendly fractions, add-recipe-to-list (with auto-create), shopping-list aisle grouping (`Ingredient.Category`), quick-add parsing, optimistic checkbox toggle with strikethrough + server rollback on failure, source-recipe badges, Clear Checked, Share List (clipboard copy of `/shopping-lists/shared/<token>`), read-only shared route (no quick-add/delete, disabled checkboxes), dark/light theme persisted in `localStorage`.

## 4. Bugs found & fixed during migration

1. **Async read inside `$effect` breaks dependency tracking.** The home page's debounce originally read the search input after an `await`, so Svelte never re-ran the effect on keystrokes (search silently stopped working). Fix: read state synchronously in the effect body. This pitfall is now documented in `AGENTS.md`.
2. **Clickable `<li>` a11y.** `svelte-check` flagged the category sidebar's `onclick` list items (and rejected `role="button"`/`tabindex` workarounds on `<li>`). Fix: real `<button type="button" class="category-item">` inside each `<li>`; CSS retargeted accordingly. Result: **0 errors / 0 warnings** across 185 files.
3. **Stale API binary.** After the `FormatNumber` change, the long-running dev API still returned raw decimals until restarted — caught by the curl verification step.

## 5. Verification

| Check | Command | Result |
|---|---|---|
| Backend build | `dotnet build api/RecipeApp.sln` | 0 errors |
| Backend fractions | `curl /api/recipes` (via proxy) | 172 fraction strings, 0 raw decimals (§2) |
| Type/a11y check | `npm --prefix client run check` | **0 errors, 0 warnings** (185 files) |
| Static SPA build | `npm --prefix client run build` | adapter-static wrote `client/dist` with `index.html` fallback |
| Dev proxy + full UI | Playwright headless Chromium against Vite dev server (`:5173`, all API traffic through the `/api` proxy) | **43/43 assertions, 2 consecutive runs** |

E2E coverage (all through the Vite proxy): top nav + theme toggle; recipe grid population (76 recipes); category sidebar filter + All-Recipes restore; debounced search (`alfredo` → single card) and clear; Add Recipe link; detail page name/scale buttons/2x scaling with fraction output/Add-to-List/Edit+Delete; form add/remove ingredient rows, submit → navigate to detail, created ingredient shows `2/3`, edit-mode prefill; delete confirm → home; New List create → detail; quick-add bar parses `2/3 cup e2e flour` into a `2/3` quantity; aisle grouping; optimistic toggle adds `.checked` + strikethrough; Add-to-List success message; source-recipe badge; Clear Checked (with count) removes checked items; Share List copies link; list has `shareToken`; shared route renders read-only (no quick-add, disabled checkboxes, no delete buttons, badges intact); theme toggle flips `data-theme`, persists to `localStorage`, survives reload; list delete via confirm; **zero uncaught page errors** across the whole run.
