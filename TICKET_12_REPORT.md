# TICKET_12 Report — First-Class Store Management

**Date:** 2026-10-25 · **Status:** DONE — build clean, `svelte-check` 0/0, deployed via Docker, curl E2E verified.

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp.Core/Models/Store.cs` | New — `Id` (Guid, EF-generated), `Name` (required, ≤ 50, case-insensitively unique), `SortOrder`, `CreatedAt` |
| `api/RecipeApp.Infrastructure/Data/RecipeDbContext.cs` | `DbSet<Store> Stores`; `Entity<Store>` config (`Name` required + max 50). No model-level index: uniqueness is a raw-SQL NOCASE index (EF 10 has no fluent collation API) |
| `api/RecipeApp.Infrastructure/Migrations/20261025171430_AddStoresTable.*` | New migration — creates `Stores`; `Up` also runs `CREATE UNIQUE INDEX "IX_Stores_Name_NOCASE" ON "Stores" ("Name" COLLATE NOCASE)` (dropped in `Down`) |
| `api/RecipeApp.Infrastructure/Data/IngredientBackfill.cs` | New `EnsureStoresAsync` — idempotently registers `DefaultStores` ("WinCo", "Costco") at canonical `SortOrder` 0/1, then any distinct non-empty names already on `Ingredients.PreferredStore` / `ShoppingListItems.Store` (appended in creation order) |
| `api/RecipeApp/Program.cs`, `api/RecipeApp/SeedData.cs` | Call `EnsureStoresAsync` after `ApplyDefaultsAsync` (seed: after the ingestion loop, so freshly seeded stores are captured too) |
| `api/RecipeApp.Core/Interfaces/IShoppingListRepository.cs` | Added `CreateStoreAsync`, `DeleteStoreAsync`; `GetKnownStoresAsync` now returns registered stores merged with ad-hoc names |
| `api/RecipeApp.Infrastructure/Repositories/ShoppingListRepository.cs` | `GetKnownStoresAsync`: registered stores by `SortOrder, Name` + ad-hoc item/ingredient names (case-insensitive merge). `CreateStoreAsync`: trim, case-insensitive duplicate check (no-op if exists), insert at `max(SortOrder)+1`, return updated list. `DeleteStoreAsync`: case-insensitive lookup, remove row, null matching `ShoppingListItems.Store` + `Ingredients.PreferredStore`, return updated list |
| `api/RecipeApp/Controllers/StoresController.cs` | `POST /api/stores` (`{name}`, trimmed, 1–50 chars else 400) and `DELETE /api/stores/{name}` — both return the updated `string[]`; existing `GET` unchanged in shape |
| `api/RecipeApp/Dtos/StoresDtos.cs` | New — `record StoreRequest(string? Name)` |
| `client/src/lib/api.js` | `createStore(name)` (POST), `deleteStore(name)` (DELETE, `encodeURIComponent`) |
| `client/src/lib/components/ShoppingListDetail.svelte` | Store bar: `+ Store` button → inline input (Enter submits, Escape cancels) that registers the store and selects its tab; active store tab shows a subtle `×` with confirm dialog — deletes the store, resets the filter to "All", and refetches the list (items are unassigned server-side). Row handlers: `handleSetStore` (PATCH), `handleNewStore` (prompt → create → PATCH assign) |
| `client/src/lib/components/ShoppingListItemRow.svelte` | Cycle-only store button replaced with `<select class="sl-store-pill">`: `""` (+ Store / unassigned), each known store, and `"__new__"` (+ New store…) which reverts the select and hands off to the parent prompt flow |
| `client/src/app.css` | `select.sl-store-pill` native-look reset + chevron; `.sl-store-add` inline form; dashed `.sl-store-add-toggle`; ghost `.sl-store-del` × (danger on hover) |

## 2. API contract

- `GET /api/stores` → `string[]`: registered stores by `SortOrder, Name`, then any unregistered names still present on items/ingredients (case-insensitive merge, alphabetical).
- `POST /api/stores` `{ "name": "…" }` → 200 with updated `string[]`; 400 if trimmed name is empty or > 50 chars; case-insensitive duplicates are a no-op.
- `DELETE /api/stores/{name}` → 200 with updated `string[]`; removes the `Stores` row (case-insensitive) and nulls every matching `ShoppingListItems.Store` and `Ingredients.PreferredStore`.

## 3. Verification (curl against `http://localhost:5051`, Docker)

| Check | Result |
|---|---|
| `GET /api/stores` after fresh boot | `["WinCo","Costco"]` ✓ (startup seeding; live DB rows: WinCo=0, Costco=1, index `IX_Stores_Name_NOCASE` present) |
| `POST {"name":"Trader Joe's"}` | `["WinCo","Costco","Trader Joe's"]` ✓ |
| `POST {"name":"   "}` | 400 ✓ |
| `POST {"name":"WINCO"}` (case-insensitive dup) | list unchanged, no duplicate row ✓ |
| `PATCH .../items/{id} {"store":"Trader Joe's"}` | item `store = Trader Joe's` ✓ |
| `DELETE /api/stores/Trader%20Joe's` | `["WinCo","Costco"]`; item reset to `store: null` ✓ (test item restored to WinCo afterwards) |
| SPA + images | `/` → 200 text/html; `/api/images/{id}/{file}` → 200 image/jpeg (RecipeImages table untouched by the migration) ✓ |

Builds: `dotnet build api/RecipeApp.sln` 0 errors · `npm --prefix client run check` 0 errors / 0 warnings · `npm --prefix client run build` OK.

## 4. Gotchas found along the way

1. **EF Core 10 removed fluent collation.** `PropertyBuilder.HasCollation` no longer exists, and there is no public `UseCollation` builder API either (`IColumn.Collation` metadata only). Case-insensitive uniqueness on SQLite is done with a raw-SQL per-index-column collation: `CREATE UNIQUE INDEX … ("Name" COLLATE NOCASE)`.
2. **EF Core 10 removed `EF.Functions.ToLower(string)`** (and `ToLowerInvariant`) from the relational `DbFunctions` surface. Case-insensitive matching in queries is done in memory here — fine at this data scale; if it ever hurts, compare against a precomputed lower-case column or raw SQL.
3. **`OrderBy/ThenBy` with an `IComparer` is not translatable** (500: "could not be translated"). Server-side ordering uses plain `.ThenBy(s => s.Name)`; case-insensitive ordering happens client-side after `ToListAsync`.
4. **EF table-name inference depends on the DbSet property.** An entity with no `DbSet<T>` property falls back to the CLR type name (`RecipeImage`, singular). During this ticket an edit accidentally *replaced* the `DbSet<RecipeImage> RecipeImages` line, which produced a phantom `RenameTable "RecipeImages" → "RecipeImage"` in the generated migration — restored the line and the diff was clean again. Keep DbSet property names as the source of truth for table names; don't let them get clobbered by unrelated edits.
