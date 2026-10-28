# TICKET 2 REPORT — Shopping Lists

Date: 2026-10-23. All five steps executed sequentially; backend + frontend verified live.

## Step 1 — Normalization fixes (`RecipeApp.Core`)

Most of the ticket's parsing work (container-unit promotion, prep-participle stripping with the full-phrase exception list, `fl oz` handling) was already present as uncommitted Ticket 1 work in `IngredientText.cs`. The one confirmed gap: the **no-quantity branch** of `SplitLine` returned before applying leading prep-participle stripping.

- Added shared helper `StripLeadingPrep(name, ref notes)`; both quantity paths now use it.
- Classifier already ordered `diced tomatoes` / `crushed tomatoes` / `stewed tomatoes` / `tomato paste` → `Canned & Jarred` before generic tomato → `Produce`; verified in DB.

Verified with a 15-case throwaway parse harness (all pass) + re-seed:
`rm -f data/recipes.db && dotnet run --project api/RecipeApp -- seed` → **76 added, 0 failed**; DB now has **351 ingredients / 620 recipe_ingredients**.

| Input | Result |
|---|---|
| `1 (14.5 oz) can diced tomatoes (undrained)` | qty 1, unit `can`, notes `(14.5 oz); (undrained)` |
| `2 (8 oz) cans tomato sauce` | qty 2, unit `can` |
| `Grated Cheese` (no quantity) | name `Cheese`, notes `Grated` (source casing preserved) |
| `diced tomatoes` / `tomato paste` | category `Canned & Jarred` |

Conventions confirmed as intended: notes keep source casing (`Grated`), parentheticals keep parens.

## Step 2 — Domain, aggregation, persistence

- **Models** (`RecipeApp.Core/Models/`): `ShoppingList` (Id, Name, ShareToken unique, CreatedAt, UpdatedAt, Items) and `ShoppingListItem` (Id, ShoppingListId, IngredientId?, Name, Category, Quantity?, Unit?, Notes?, RecipeSources?, IsChecked, SortOrder). No `Guid.NewGuid()` in constructors; EF generates keys.
- **`ShoppingListAggregator`** (pure domain, no EF): merges by normalized name + unit group.
  - Volume: accumulate in tsp; ≥ 12 tsp → tbsp, ≥ 0.25 cup → cups (`3 tsp + 1 tbsp = 4 tbsp`, `16 tbsp = 1 cup`).
  - Weight: accumulate in oz; ≥ 16 oz → lb.
  - Incompatible units stay separate (e.g. `0.5 cup butter` and `2 oz butter` are two rows — volume vs weight is ambiguous).
  - Notes deduped case-insensitively, joined with `"; "`; recipe titles tracked in `RecipeSources` (comma-separated, deduped).
- **Persistence**: `DbSet`s + indexes (unique ShareToken, cascade list→items, SetNull item→ingredient) in `RecipeDbContext`; `IShoppingListRepository`/`ShoppingListRepository` registered scoped in DI; migration **`20261023025335_AddShoppingLists`** created and applied (`ShoppingLists`, `ShoppingListItems` tables exist).

## Step 3 — API (`/api/shopping-lists`)

| Endpoint | Result (live curl) |
|---|---|
| `GET /` | summary: `('Weekend Cook', 29 items, 1 checked)` ✓ |
| `GET /{id}` | full list ordered Category → IsChecked → Name ✓ |
| `GET /shared/{token}` | identical to detail view (29/29 items) ✓ |
| `POST /` | 201; empty or pre-populated via raw-text items ✓ |
| `POST /{id}/add-recipes` | Alfredo ×1 + Bourbon Chicken ×2 + Bangers and Mash ×1 → 28 merged items; `4 lb boneless chicken breasts`, `2 garlic clove` (×2 scaling) ✓ |
| `POST /{id}/items` | `"2 gallons whole milk"` → `2 gal whole milk`, `Dairy & Eggs` ✓ |
| `PATCH /{id}/items/{itemId}` | toggle `isChecked` ✓; edit quantity `1 → 2.5 cup` ✓; name/category edit ✓ |
| `DELETE /{id}/items/checked` | removed only the checked item (29 → 28) ✓ |
| `DELETE /{id}/items/{itemId}` | 200, list shrinks ✓ |
| `DELETE /{id}` | 204; summary back to 0 lists ✓ |

Design note: re-adding a recipe that already contributed is a **no-op** (checked against `RecipeSources`), so the "Add to Shopping List" button can be tapped repeatedly without double-counting.

## Step 4 — Frontend (`client/`)

- **`api.js`**: 10 new functions covering every endpoint.
- **`TopNav.jsx`** + `App.jsx`: sticky top nav with **Recipes** / **Shopping Lists** links (active state, wraps on mobile); routes `/shopping-lists`, `/shopping-lists/:listId`, `/shopping-lists/shared/:token`.
- **`ShoppingListsPage.jsx`**: summary rows (items/checked counts, updated date), New List, Delete.
- **`ShoppingListDetail.jsx`**: quick-add bar (parsed server-side via `SplitLine` + classifier), items grouped by aisle with per-category counts, **optimistic checkbox toggling** (strikethrough + muted, rollback on failure), `from <Recipe>` source badges, Clear Checked, Share List (copies `/shopping-lists/shared/:token`, prompt fallback). Shared route renders the same view read-only for grocery-store phone use.
- **`RecipeDetail.jsx`**: scale control `0.5x / 1x / 2x / 3x` scaling displayed quantities + **Add to Shopping List** (uses selected multiplier, auto-creates a default list when none exist, confirms with "Added to … at 2x").
- **DTO sync**: `IngredientDto` extended with numeric `amount` + `unit` (display `quantity` string kept) so the client can scale; `dataModels.js` typedefs updated.
- **`App.css`**: top nav, list rows, aisle groups, checked styling, badges, scale buttons — all via existing theme variables (light/dark).

## Step 5 — Verification

- `dotnet build api/RecipeApp.sln` → **0 errors** (10 pre-existing warnings).
- `npm --prefix client run build` → ✓ (52 modules); `npm --prefix client run lint` → **clean**.
- E2E against the live API: full sequence above (create → 3 overlapping recipes → manual item → toggle → clear checked → shared link → edits → deletes), all green.

**UI limitation:** no Chromium/Chrome available in this environment, so the React surface could not be driven in a real browser. Smoke checks performed instead: dev server serves `/shopping-lists` (200 SPA fallback), the Vite proxy chain returns API JSON at `/api/shopping-lists`, and the production bundle contains the new routes/components (`shopping-lists/shared`, `Add to Shopping List`, `Clear Checked`, `sl-badge`).

## Data notes

- `0.6666 cup light brown sugar` in aggregated output is a seed-data artifact: `1/3` was stored rounded at parse time, then ×2 — not an aggregator rounding bug.
- Mid-line parentheticals (e.g. Bangers and Mash's mustard line) stay in the name; note extraction covers leading/trailing parentheticals per Ticket 1 scope.
