# Roadmap & Status — Family Recipes App

> Living document. Update this file as work progresses so any future session can pick up where the last one left off. Full original plan: `PROJECT_PLAN.md`.

## Current status (2026-09-23)

**Goal for this work session:** get the app to a *usable* state = Phase 2 milestone from PROJECT_PLAN.md: all recipes from `Recipes.md` loaded into Postgres, browsable/searchable/editable through the React UI against the real API.

**Status: DONE (2026-09-23).** All 76 recipes seeded into local Postgres; React UI fully wired to the API (no mock data); browse/category/detail/create/edit/delete verified via curl and browser E2E, including SQL-level confirmation of edit persistence.

### What exists
- **Data (complete):** `Recipes.md` — 76 recipes in 7 categories, parsed deterministically by `extract_recipes.py` (no LLM). All 76 JSON files regenerated in `recipes_json/`; every recipe has ingredients + instructions except `Mac and Cheese Variations` (a notes-only entry in the source — intentional, no ingredient list exists there).
- **Backend skeleton:** layered solution (`Core` / `Infrastructure` / `RecipeApp` host), EF Core schema with normalized `Ingredient` + `RecipeIngredient` bridge, ordered `RecipeStep`s, category M2M. `Quantity` made nullable (many ingredient lines have no quantity).
- **Frontend skeleton:** React 19 + Vite + router; list/detail/form/category components existed but were mock-data only.

### Decisions log
- **Postgres** via Docker (`postgres.sh`, port 5432) — matches existing setup; plan's SQL Server option dropped.
- **Bug fix (2026-09-23): recipe edit returned 500.** All five domain models initialized `Id = Guid.NewGuid()` in their constructors, so EF tracked new child entities (`RecipeIngredient`/`RecipeStep`) as `Modified` instead of `Added` — PUT issued UPDATEs against non-existent rows → `DbUpdateConcurrencyException`. Fix: removed the constructor GUID initializers from all five models; EF generates keys at save time. Verified end-to-end (curl PUT round-trip, SQL log shows correct DELETE+INSERT, browser E2E edit with add/remove ingredient + description change persists).
- **Deterministic extraction, no LLM.** The markdown format is regular enough to parse with plain rules; the old `openai` dependency is gone.
- **Parser rules (`extract_recipes.py`):** quantity-starting lines and short colon-terminated labels before any step (e.g. `Bread Dough:`) count as ingredients; sentence-like lines (terminal punctuation, no quantity) count as instructions. Literal `Ingredients`/`Instructions`/`Directions` header lines split the section when present. Rules are tuned to this corpus — re-run `python3 extract_recipes.py --dry-run` after any `Recipes.md` edit and eyeball the dump.
- **Quantity/unit/notes splitting happens in the C# seeder**, not Python — keeps domain mapping in one place (C#), JSON stays a raw import format.
- **No React Query yet.** Plain fetch + hooks for v1 usability; add TanStack Query when caching/refetch pain appears.
- **Dev server proxy** (`vite.config.js` → `/api`) instead of CORS for local dev.
- **Environment note (2026-09-22):** this machine is plain Linux, not WSL2 — no `powershell.exe`. .NET 10 SDK installed to `~/.dotnet` via dotnet-install.sh; use `$HOME/.dotnet/dotnet` (or add to PATH). CLAUDE.md's WSL instructions are stale for this box.

## Phase 2 execution checklist

### Data pipeline
- [x] Audit old extraction: 31/74 covered, anchor leaks, empty file
- [x] Delete old `recipes_json/*.json`
- [x] Rewrite `extract_recipes.py` deterministically (no LLM)
- [x] Extract all 76 recipes; validate every recipe has ingredients + instructions; spot-check output

### API
- [x] Make `RecipeIngredient.Quantity` nullable (`decimal?`)
- [x] EF Core migration `InitialCreate`
- [x] `RecipesController`: GET list (with `?q=` search, `?category=` filter), GET by id, POST, PUT, DELETE; `CategoriesController`: GET list with recipe counts
- [x] Seed command: `dotnet run --seed <path-to-recipes_json>` — upserts categories/ingredients/recipes from JSON, splits quantity/unit/notes
- [x] Build passes; API runs against local Postgres (port 5051)

### Client
- [x] Vite dev proxy `/api` → API port
- [x] Replace `mockData.js` with fetch layer (`src/api.js`); delete mock file
- [x] Recipe list: cards from API, category sidebar from API, debounced search box → `?q=`
- [x] Recipe detail: ingredients (qty + unit + name + notes), numbered steps, categories
- [x] Recipe form (add/edit) against POST/PUT with dynamic ingredient rows

### Verification
- [x] Postgres container up; seed runs; all 76 recipes in DB
- [x] curl checks: list, search, detail, create, update, delete round-trip
- [x] Browser E2E: browse → category filter → search → detail → add recipe → edit (add/remove ingredient + description) → delete
- [x] Commit work; this file reflects final state

## Remaining roadmap (after usable state)

| Phase | Summary | Notes |
|---|---|---|
| 3 · Auth/users | ASP.NET Identity + JWT, family invite flow, per-user ownership | Needs `UserId` FK on Recipes — schema change |
| 4 · Shopping lists | Generate from recipes, dedupe ingredients, shareable read-only URL | Depends on ingredient normalization quality (seed splitting) |
| 5 · Meal planning | Weekly grid, drag-and-drop, week → shopping list | @dnd-kit |
| 6 · Polish/scale | Photos, PWA, ratings/notes, import from URL, FTS, backups, E2E tests | Prioritize by family usage |

See `PROJECT_PLAN.md` for full detail.

## Known gaps / follow-ups
- No test project yet (plan called for `RecipeApp.Tests`) — add when endpoints stabilize.
- No CI/CD, no docker-compose file (raw `docker run` in `postgres.sh`).
- Ingredient category (`Ingredient.Category`: produce/dairy/pantry) not populated by seed — needs a word list or LLM pass later; blocks Phase 4 grouping.
- Recipe metadata (servings, prep/cook time, cuisine) absent from source markdown — null until entered via UI.
