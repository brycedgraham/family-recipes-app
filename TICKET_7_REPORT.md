# TICKET_7 Report — Multi-Store Routing (WinCo / Costco) & Pantry Staples

**Date:** 2026-10-24 · **Status:** DONE — `dotnet build` green (only pre-existing NuGet advisory warnings), `svelte-check` 0 errors / 0 warnings (188 files), adapter-static SPA build green, curl E2E against the live app on `:5051` passing, and a DOM-level E2E that mounts the real compiled `ShoppingListDetail` component against the live API — **21/21 assertions** passing.

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp.Core/Models/Ingredient.cs` | + `PreferredStore` (`string?`, max 50) and `IsPantryStaple` (`bool`) |
| `api/RecipeApp.Core/Models/ShoppingListItem.cs` | + `Store` (`string?`, max 50) and `IsPantryStaple` (`bool`) |
| `api/RecipeApp.Core/IngredientDefaults.cs` | New — pure-domain defaults: the 13 pantry-staple names (`water`, `warm/cold/boiling/ice water`, `salt`, `kosher salt`, `sea salt`, `black pepper`, `pepper`, `cooking spray`, `vegetable oil`, `canola oil`), `DefaultStores = ["WinCo", "Costco"]`, and store-hint detection — name or notes containing `winco` → `"WinCo"`; `costco`/`kirkland` → `"Costco"` (case-insensitive) |
| `api/RecipeApp.Core/ShoppingListAggregator.cs` | `AddManualItem` / merge paths accept + apply `preferredStore` and `isPantryStaple`; merging an existing line keeps its already-assigned store, only fills in a missing one, and ORs the pantry flag upward |
| `api/RecipeApp.Core/Interfaces/IRecipeRepository.cs` · `IShoppingListRepository.cs` | + `UpdateIngredientPreferencesAsync(...)` (catalog memory) and `GetKnownStoresAsync()` |
| `api/RecipeApp.Infrastructure/Data/RecipeDbContext.cs` + migration `20261024015905_AddStoreAndPantrySupport` | New columns on `Ingredients` (`PreferredStore`, `IsPantryStaple`) and `ShoppingListItems` (`Store`, `IsPantryStaple`) |
| `api/RecipeApp.Infrastructure/Data/IngredientBackfill.cs` | New — startup backfill over existing `Ingredients`: applies store hints and pantry-staple flags to rows that don't already have them (idempotent, only touches what changed) |
| `api/RecipeApp.Infrastructure/Repositories/RecipeRepository.cs` | `FindOrCreateIngredientAsync` now applies `IngredientDefaults` on both the create and the existing-ingredient path; implements `UpdateIngredientPreferencesAsync` (resolves by `IngredientId`, falls back to `NormalizedName` match) |
| `api/RecipeApp.Infrastructure/Repositories/ShoppingListRepository.cs` | Implements `GetKnownStoresAsync`: `["WinCo", "Costco"]` + any custom store names present in `Ingredients.PreferredStore` or `ShoppingListItems.Store` (case-insensitive dedupe, defaults first) |
| `api/RecipeApp/Controllers/ShoppingListsController.cs` | `PATCH .../items/{id}` accepts optional `store` (`""`/null clears) and `isPantryStaple`; when either is sent it **persists back to the ingredient catalog** via `UpdateIngredientPreferencesAsync`. Quick-add `POST .../items` accepts an optional `store` that wins over the catalog default |
| `api/RecipeApp/Controllers/StoresController.cs` | New — `GET /api/stores` → `string[]` |
| `api/RecipeApp/Dtos/ShoppingListDtos.cs` | + `Store`/`IsPantryStaple` on the item DTO; + `Store`/`IsPantryStaple` on `PatchItemRequest` |
| `api/RecipeApp/Program.cs` | Runs `IngredientBackfill.RunAsync` at startup after migrations (before seeding) |
| `client/src/lib/api.js` | + `fetchStores()`; `addShoppingListItem(id, rawText, store)` and `patchShoppingListItem` now carry the new fields |
| `client/src/lib/components/ShoppingListDetail.svelte` | Store filter bar, per-item store pills, quick-add store defaulting, collapsible pantry section (all Svelte 5 runes; rows extracted to the component below) |
| `client/src/lib/components/ShoppingListItemRow.svelte` | New — one list row: checkbox, quantity, name, **store pill** (`WinCo` / `Costco` / `+ Store`, one tap cycles unassigned → WinCo → Costco → unassigned), **pantry button** (`✓ pantry` / `+ pantry`), notes, recipe-source badges, remove |
| `client/src/app.css` | Styles for `.sl-storebar`, `.sl-store-pill`, `.sl-pantry-btn`, `.sl-pantry` section |

## 2. Store memory semantics

- **Inheritance:** when a list is aggregated from recipes or an item is quick-added, the item gets `Store = Ingredient.PreferredStore` (an explicit store — e.g. from the active filter tab on quick-add — wins).
- **Merging:** items for the same ingredient in the same list merge regardless of store; the existing line keeps its already-assigned store, a missing one is filled from the catalog.
- **Persistent memory:** `PATCH` with `store` (or `isPantryStaple`) on an item that links to the catalog also updates `Ingredient.PreferredStore` / `Ingredient.IsPantryStaple`. Assigning "boneless chicken breasts" to Costco once therefore routes it to Costco on every future weekly list. Verified end-to-end: a brand-new list built from a recipe containing that ingredient arrived with `store: "Costco"` automatically.
- **Clearing:** sending `store: ""` clears both the item and the catalog default; pantry toggles persist symmetrically (users can customize what counts as a staple).

## 3. Pantry staples

Defaults come from `IngredientDefaults.IsPantryStaple` (13 canonical names, matched on `NormalizedName`). Applied at seed time for new ingredients and by an idempotent startup backfill over the existing catalog (`data/recipes.db` currently has water/salt/pepper/vegetable oil flagged). In the UI, **unchecked** pantry items are pulled out of the aisle groups into a collapsible **"Pantry Check — Already Have? (N)"** section at the bottom; checking an item off hides it from the section, and each row's `+ pantry` button lets the user add/remove staples (persisted to the catalog).

## 4. UI behavior (`ShoppingListDetail.svelte`)

- **Store filter bar:** `All (N)` | `WinCo (X)` | `Costco (Y)` | `Any / Unassigned (Z)`, plus any custom store in use (from `GET /api/stores` or already on items). Selecting a store filters both the aisle groups and the pantry section to that store.
- **One-tap store pill:** optimistic update + `PATCH`; the same request saves the ingredient's default for future weeks.
- **Quick-add under a store tab:** newly added items default to that store (e.g. adding "bow tie pasta" while the Costco tab is active → item arrives with `store: "Costco"`).
- **Pantry section:** collapsible via its header button; hidden entirely when no unchecked staples match the active filter.

## 5. Verification

1. **Builds:** `dotnet build api/RecipeApp.sln` (0 errors), `npm --prefix client run check` (0 errors / 0 warnings, 188 files), `npm --prefix client run build` (green).
2. **curl E2E against `:5051`:** generated the weekly list from the meal plan (`water`, `salt`, `pepper`, `vegetable oil` flagged pantry by default); assigned boneless chicken breasts → Costco and bow tie pasta → WinCo via `PATCH`; confirmed catalog persistence in SQLite (`Ingredients.PreferredStore = 'Costco'`, paprika `IsPantryStaple = 1` after a manual toggle); created a fresh list from the Bourbon Chicken recipe — its chicken item **automatically inherited `store: "Costco"`**.
3. **DOM E2E (21/21):** this environment's headless Chromium builds (both `chromium-1248` and `chromium_headless_shell`) abort with a Skia fatal — `SkFontMgr_FontConfigInterface.cpp:163 Not implemented` — because the system has no fontconfig and both binaries ship an unimplemented fontconfig stub, so a pixel-level Playwright run was not possible. Instead the real component (compiled by Vite in client mode) was mounted inside jsdom against the live API: tab counts (`All (22) / WinCo (1) / Costco (1) / Any / Unassigned (20)`), pantry section header/count/items, per-store filtering of main groups **and** the pantry section, pill cycling with optimistic + persisted state, quick-add store defaulting, and pantry collapse/expand — all asserted on the rendered DOM plus re-fetched API state.

## 6. State left in the app (intentional)

The weekly list "Weekly list: Week of Oct 19, 2026" (`cfadf6e6…`) is kept as a live demo: boneless chicken breasts → Costco, bow tie pasta → WinCo, and paprika marked as a pantry staple — including the matching catalog memory (chicken = Costco, pasta = WinCo). A beef stew entry was added to that week's meal plan as the E2E vehicle.
