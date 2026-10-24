# AGENTS.md — Family Recipes App

Canonical guide for AI coding agents working in this repository. Read `PLAN.md` (canonical plan, status, and backlog) before large changes. This file wins on conflicts with stale docs.

## Architecture & Stack

- **Backend:** ASP.NET Core 10 Web API, layered solution in `api/`:
  - `RecipeApp` — host: DI wiring, controllers, DTOs, seed command.
  - `RecipeApp.Core` — pure domain: models, interfaces, parsing (`IngredientText`). Zero infrastructure dependencies.
  - `RecipeApp.Infrastructure` — EF Core `DbContext`, repositories, migrations.
- **Database:** **SQLite** at `data/recipes.db` (repo-relative; the API rewrites relative `Data Source` paths against the repo root via `SqliteDatabase.Resolve`). Zero-container local dev — no Docker. Connection string lives in `api/RecipeApp/appsettings.json` under `ConnectionStrings:DefaultConnection`. EF Core migrations remain the schema source of truth — add a new migration when changing the schema; do not hand-edit old ones. (Migrated from PostgreSQL/Npgsql on 2026-10-16; the Npgsql migration was replaced by a fresh SQLite `InitialCreate`.)
- **Frontend:** SvelteKit (Svelte 5) SPA in `client/` — `@sveltejs/adapter-static` with `fallback: 'index.html'`, `export const ssr = false;` in `src/routes/+layout.js`. Plain fetch via `src/lib/api.js`; fraction-aware quantity display via `src/lib/format.js` (mirrors the C# `IngredientText.FormatNumber`). Dev server proxies `/api` → `http://localhost:5051`. In production the .NET API serves the built SPA itself from `client/dist` (unmatched non-API routes fall back to `index.html`; unknown `/api/*` still 404s as JSON) — see `start.sh`. Installable PWA: `client/static/manifest.webmanifest` + icons, linked from `src/app.html`.
- **Ingestion:** Python 3.12, single script `extract_recipes.py` — deterministic parser, no LLM, no third-party deps.

## Image Storage Strategy

Recipe images are stored on the **local filesystem** under `data/images/{recipeId}/` (one file per image, named `{guid}{ext}`) and served by the API at `/api/images/{recipeId}/{fileName}` via a `PhysicalFileProvider` in `Program.cs` — serving under `/api/...` keeps the Vite dev proxy working for images too. SQLite stores only the **metadata** (`RecipeImage`: `FileName`, `SortOrder`, `CreatedAt`) — never binary blobs in the DB. The client's `imageUrls` field holds API-relative URLs. Uploads are validated (extensions `.jpg/.jpeg/.png/.webp/.gif`, ≤ 10 MB). Add `data/` to `.gitignore` except for a `.gitkeep`.

## Layer Rules

- Pure domain logic and text parsing (ingredient splitting, quantity math) → **`RecipeApp.Core`**. No EF Core, no ASP.NET types there.
- EF Core context, queries, repositories, migrations → **`RecipeApp.Infrastructure`**.
- HTTP routing, controllers, request/response DTOs, mapping → **`RecipeApp`** (host project).
- Repository pattern: interface in `Core/Interfaces`, implementation in `Infrastructure/Repositories`, registered scoped in DI.
- **Never assign `Guid.NewGuid()` in model property initializers or constructors.** EF must generate keys at save time; constructor GUIDs make new child entities (`RecipeIngredient`/`RecipeStep`) track as `Modified` and break PUT (this was a real 500 bug — see PLAN.md §5).

## Svelte 5 Runes Rules (Strict)

All components in `client/src` MUST use Svelte 5 runes. The legacy Svelte 4 syntax is banned:

| Concern | Required (runes) | Banned (Svelte 4) |
| --- | --- | --- |
| State | `let items = $state([]);` | top-level implicit reactivity / `$:` state |
| Derived | `let filtered = $derived(...);` | `$:` labels |
| Effects | `$effect(() => { ... });` | `$:` side-effects |
| Props | `let { recipe, onDelete } = $props();` | `export let` |
| Events | native attributes: `onclick={...}`, `onsubmit={...}`, `oninput={...}` | `on:click`, `on:submit`, `on:input` |
| Layout children | `{@render children()}` in `+layout.svelte` | `<slot />` |

Additional conventions:

- Read state **synchronously** inside `$effect` bodies so dependencies are tracked (an async read, e.g. after `await`, is not tracked and the effect will never re-run — real bug fixed during the React→Svelte migration).
- Interactive list items must be real `<button type="button">` elements (keyboard-accessible); `svelte-check` warns on clickable `<li>`/non-interactive elements with event listeners. Keep `npm --prefix client run check` at 0 errors / 0 warnings.

## Commands

```bash
# .NET (SDK 10 required; if missing, install via dotnet-install.sh to ~/.dotnet)
dotnet build api/RecipeApp.sln                 # build backend
dotnet run --project api/RecipeApp             # run API at http://localhost:5051
dotnet run --project api/RecipeApp -- seed     # import recipes_json/*.json (idempotent; skips existing titles)
dotnet test api/RecipeApp.sln                  # no test project exists yet — create RecipeApp.Tests (xUnit) when adding tests

# Production (single process: UI + API + SQLite + images on one port)
./start.sh                                 # builds client/dist + Release API, serves http://0.0.0.0:5051

# Client
npm --prefix client install                    # first time
npm --prefix client run dev                    # Vite dev server (proxies /api → :5051)
npm --prefix client run check                  # svelte-check (must stay 0 errors / 0 warnings)
npm --prefix client run build                  # static SPA build to client/dist (adapter-static, fallback index.html)

# Ingestion (regenerate recipes_json/ from Recipes.md — never edit generated JSON by hand)
python3 extract_recipes.py --dry-run           # preview parse without writing
python3 extract_recipes.py                     # write all recipes_json/*.json
```

## Version Control

- **Commit incrementally, per verified deliverable.** Before starting a new deliverable (the next `[ ]` in `PLAN.md`), the working tree MUST be clean — commit all prior verified work first. This is what makes cold-start handoffs and rollbacks safe.
- **Message style:** `<type>: <what changed>` + ticket reference for feature work, e.g. `feat: TICKET_7 multi-store routing + pantry staples`. Types: `feat`, `fix`, `docs`, `refactor`, `chore`. One commit per logical unit of work (a deliverable spanning backend + frontend is one commit).
- **Never commit runtime/generated state:** `data/` (SQLite DB + recipe images), `client/dist/`, `client/.svelte-kit/`, `node_modules/`, `bin/`/`obj/`, `.env` — all gitignored. Run `git status` before committing and confirm nothing under `data/` is staged.
- **Rollback:** prefer `git revert <sha>` (safe inverse commit); use `git reset --hard <sha>` only when you are sure the uncommitted work can be discarded.

## Conventions & Gotchas

- `recipes_json/` is **generated** (gitignored). Source of truth for recipe content is `Recipes.md`; after editing it, re-run the extractor with `--dry-run` and eyeball the output.
- Ingredient line splitting (`Quantity`/`Unit`/`Name`) happens in C# at seed time (`IngredientText.SplitLine`), not in Python — keep domain mapping in one place.
- Normalization runs in C# at seed time (`IngredientText.SplitLine` + `IngredientCategoryClassifier`): canonical units via an alias table (`tbsp`/`T`/`tablespoons` → `tbsp`, …), quantity parsing for fractions/decimals/ranges (`5-6 eggs`, `1 or 2 bay leaves`, `3 / 4 cup`), notes extraction (semicolon clauses, leading/trailing parentheticals, comma prep phrases, size qualifiers like `9 oz. bag`), container-word stripping from names, and a grocery-aisle `Ingredient.Category`. `NormalizedName = name.ToLowerInvariant()` (unique index) drives cross-recipe dedup. Details + before/after samples: `TICKET_1_REPORT.md`.
- DTOs intentionally mirror the client's data model (Svelte components consume them directly via `src/lib/api.js`): recipe `name`, categories as string list, ingredient quantity as display string. Keep both sides in sync when changing shapes.
- **Middleware order in `Program.cs` is load-bearing for image serving.** The static-file middleware (`client/dist` + `/api/images`) must run *before* an explicit `app.UseRouting()`, which must come before `MapControllers`. Without the explicit `UseRouting`, .NET 10 lets endpoint matching — including the `Map("/api/{**rest}")` catch-all — beat `UseStaticFiles`, and every `/api/images/...` request 404s as JSON instead of serving the file (real bug found during TICKET_5 E2E). New middleware goes before `app.UseRouting()`; new endpoints after it.
- Stale file: `api/RecipeApp/RecipeApp.http` (weatherforecast template) — clean up or ignore.
- Secrets: `.env` is gitignored and no longer read by anything (leftover LLM/DB vars from the old stack). The SQLite connection string in `appsettings.json` is a local path — no credentials.
