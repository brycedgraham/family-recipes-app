# TICKET_4 Report — Meal Planner (Weekly Grid + Week → Shopping List)

**Date:** 2026-10-23 · **Status:** DONE — `dotnet build` 0 errors, `svelte-check` 0 errors / 0 warnings (187 files), adapter-static SPA build green, Playwright E2E **31/31 assertions** passing.

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp.Core/Models/MealPlan.cs` | New — `MealPlan` (one row per ISO week, `WeekStartDate` normalized to Monday, unique) + `MealPlanEntry` (`DayOfWeek` 0=Mon…6=Sun, `MealType`, nullable `RecipeId` or `CustomTitle`, `Multiplier` decimal scale, `Notes`, `SortOrder`) |
| `api/RecipeApp.Core/Interfaces/IMealPlanRepository.cs` · `api/RecipeApp.Infrastructure/Repositories/MealPlanRepository.cs` | New — get-or-create week, add/update/delete entries (entry DTOs resolve `recipeTitle`); registered scoped in `Program.cs` |
| `api/RecipeApp/Dtos/MealPlanDtos.cs` | New — week/entry DTOs + request types |
| `api/RecipeApp/Controllers/MealPlansController.cs` | New — `api/meal-plans` endpoints (§2) |
| `api/RecipeApp.Infrastructure/Migrations/20261023121340_AddMealPlans.cs` | New migration — `MealPlans` + `MealPlanEntries`; unique index on `WeekStartDate`; FK → `MealPlans` cascade, FK → `Recipes` **SET NULL** (deleting a recipe keeps its scheduled meals as custom-titled entries) |
| `client/src/routes/meal-plan/+page.svelte` | New — 7-day Mon–Sun grid page (§3) |
| `client/src/lib/dates.js` | New — local-time-safe date helpers (`parseISO`, `toISO`, `mondayOf`, `addDays`, `todayISO`, `fmtLong`); no UTC-parsing pitfalls |
| `client/src/lib/api.js` | +5 functions: `fetchMealPlanWeek`, `addMealPlanEntry`, `patchMealPlanEntry`, `deleteMealPlanEntry`, `buildMealPlanShoppingList` |
| `client/src/lib/components/RecipeDetail.svelte` | New **"Add to Meal Plan"** popover next to the scale row — day/meal selects + qty input, prefilled with the current scale; success message "Added to Fri Dinner" |
| `client/src/routes/+layout.svelte` | Top nav gains a **Meal Plan** link |
| `client/src/app.css` | `.mp-*` styles: week header/nav, 7-column grid (→2 cols ≤932px, →1 col ≤576px), day cells with today highlight, entry cards (meal-type chip, title link, multiplier badge, notes, edit/remove), add/edit modal + overlay |

## 2. API surface (`api/meal-plans`)

| Endpoint | Behavior |
|---|---|
| `GET /week/{date}` | Any date normalizes to its ISO-week Monday; returns the plan or an **empty virtual plan** (no row created for empty weeks) |
| `POST /week/{date}/entries` | Auto-creates the week's plan (`"Week of Oct 19, 2026"`); validates meal type (Breakfast/Lunch/Dinner/Snack), recipe existence, and requires a recipe **or** a custom title; returns the updated week |
| `PATCH /entries/{entryId}` | Update day/slot/recipe/custom title/multiplier/notes — this is how a meal is **moved** to another day (no drag-and-drop in v1) |
| `DELETE /entries/{entryId}` | Remove an entry; returns the updated week |
| `POST /week/{date}/shopping-list` | Collects every **recipe** entry, feeds each into the existing shopping-list ingredient pipeline at its multiplier, and merges into an existing list (optional `listId`) or a new one. Repeated recipes **sum**: `1x + 2x` of the same recipe → 3× quantities. Custom meals are skipped |

Validation verified via curl: bad meal type → 400, non-existent recipe id → 404, entry with neither recipe nor custom title → 400, malformed date → 400.

## 3. Frontend — `/meal-plan`

- **Header:** `Week of <Monday>` label, ‹ / › week navigation (`?week=` param; API re-normalizes), **Build Week Shopping List** button (disabled while the week is empty).
- **Grid:** 7 day columns with today's column highlighted; each cell lists its entries and a `+ Add` button.
- **Entry card:** meal-type chip, recipe title as a link to `/recipes/{id}` (custom meals render plain text), multiplier badge only when ≠ 1 (`2x`, `1.5x`), notes line, edit (✎) and remove (✕, confirm dialog) buttons.
- **Add/Edit modal:** Day + Meal selects, Recipe select (with `— custom meal —` option that reveals a title input), multiplier (0.5 steps), optional notes; client validation ("Pick a recipe or enter a custom title."); Escape key and backdrop click close it.
- **Build:** navigates to the created/updated shopping list, which shows aisle-grouped items with summed quantities rendered as friendly fractions (ticket 3's `format.js`).

All components use strict Svelte 5 runes per `AGENTS.md` (`$state`/`$derived`/`$effect`/`$props`, native event attributes). One a11y-driven design point: the modal backdrop is a focusable `role="button"` with a target-checked click guard + document-level Escape handler, which keeps `svelte-check` at 0 warnings.

## 4. Verification

| Check | Command | Result |
|---|---|---|
| Backend build | `dotnet build api/RecipeApp.sln` | **0 errors** |
| Endpoint validation | curl against running API (meal type, recipe id, empty entry, bad date) | all 400/404 as specified |
| Type/a11y check | `npm --prefix client run check` | **0 errors, 0 warnings** (187 files) |
| Static SPA build | `npm --prefix client run build` | adapter-static wrote `client/dist` with `index.html` fallback |
| Dev proxy + full UI | Playwright headless Chromium against Vite dev server (`:5173`, all API traffic through the `/api` proxy) | **31/31 assertions** |

E2E coverage (all through the Vite proxy): nav contains Meal Plan; header shows `Week of Oct 19, 2026`; 7 day columns Mon–Sun; today's column highlighted; empty week renders no entries and disables Build; add-modal opens with Day/Meal/Recipe fields and hides the custom title once a recipe is picked; Monday Dinner Alfredo entry appears with detail link and **no** multiplier badge at 1x; Tuesday custom meal "E2E Leftovers" shows note text and no link; editing moves it to Sunday as Snack; Wednesday Alfredo 2x shows a `2x` badge; submitting an empty entry shows the validation error; Escape closes the modal; next-week navigation shows an empty following Monday (with `?week=` in the URL) and previous restores the 3 entries; **Build** creates a shopping list where Garlic Powder sums to `3 tsp` (1x+2x), Butter to `1 1/2 cup`, Salt to `3/4 tsp`, with a "from Alfredo…" source badge; recipe-detail popover adds a Friday Dinner entry at 1.5x and reports "Added to Fri Dinner"; the new entry appears on the grid with a `1.5x` badge; remove (confirm) deletes it; cleanup leaves the week empty; no uncaught page errors.

## 5. Notes / follow-ups

- **No drag-and-drop** in v1 — moving a meal is an edit (PATCH to a new day/slot). Revisit with @dnd-kit only if manual moves become annoying.
- The built shopping list reuses the existing quick-add/aggregation pipeline, so aisle grouping, dedup, and fraction display all come for free; custom (non-recipe) meals are intentionally not shopped.
- Deleting a recipe with scheduled entries keeps those entries (`RecipeId` → NULL via FK `SET NULL`) — they render as untitled custom entries until edited; acceptable v1 behavior, flag if it bites.
