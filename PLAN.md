# PLAN — Family Recipes App (Single Source of Truth)

> **This is the canonical plan and status document.** It replaces `ROADMAP.md` and
> `PROJECT_PLAN.md` (both deleted 2026-10-24). If a rule here conflicts with stale prose in an
> older ticket report, this file wins. Per-repo conventions, layer rules, Svelte 5 runes rules,
> and commands live in `AGENTS.md` — read that first (see the cold-start protocol below).

**Status: 2026-10-24.** Deliverables 1–9 complete and verified; next work is **Deliverable 10**
(automated unit tests + backup script). The app runs as one .NET process on
`http://0.0.0.0:5051` serving the SPA, API, SQLite, and recipe photos.

---

## 1. Cold-Start Handoff Protocol

Any human PM, Senior Architect AI, or local `omp.sh` agent can resume with **zero chat history**
by following these steps in order:

1. **Read `AGENTS.md`.** It defines the stack, the three-project layer rules (what goes in
   `Core` vs `Infrastructure` vs the host), the strict Svelte 5 runes conventions, every build/run
   command, and the load-bearing gotchas (middleware ordering, no constructor GUIDs, image
   storage strategy). This file wins on conflicts with stale docs.
2. **Read this file (`PLAN.md`).** Use §2 for the current architecture and §3–§4 to see exactly
   what is done and what remains.
3. **Locate the next unchecked deliverable.** Find the topmost `[ ]` in §4 (or its sub-checkboxes).
   That is the unit of work. Do not start a lower item while a higher one is open, and do not
   reorder without an explicit instruction.
4. **Implement it** following `AGENTS.md`:
   - Schema change → add an EF Core migration (`dotnet ef migrations add <Name>`); never hand-edit
     old migrations.
   - New client component/state → Svelte 5 runes only (`$state`/`$derived`/`$effect`/`$props`,
     native event attributes, `{@render children()}`); keep `svelte-check` at 0 errors / 0 warnings.
   - DTO shapes mirror the client data model — change both sides together.
5. **Verify before claiming done.** At minimum: `dotnet build api/RecipeApp.sln`,
   `npm --prefix client run check`, `npm --prefix client run build`. For behavioral changes,
   exercise the real path (curl against the API and/or a headless-browser E2E) and observe the
   result — "tests pass" alone is not proof.
6. **Close the loop.** Check off the boxes in §4 as each sub-item lands, append any non-obvious
   decision to §5 (dated), write `TICKET_N_REPORT.md` matching the style of the existing reports
   (`TICKET_1_REPORT.md` … `TICKET_5_REPORT.md`), and commit the deliverable (see `AGENTS.md` →
   Version Control) so every ticket has a clean rollback point.

**Quick runbook** (full list in `AGENTS.md`):

```bash
./start.sh                                        # production: one process on :5051 (SPA+API+images)
dotnet build api/RecipeApp.sln                    # backend build
npm --prefix client install && npm --prefix client run dev   # dev server (proxies /api → :5051)
npm --prefix client run check                     # svelte-check (must stay 0/0)
python3 extract_recipes.py --dry-run              # preview re-parse of Recipes.md
dotnet run --project api/RecipeApp -- seed        # idempotent import of recipes_json/*.json
```

---

## 2. Current Architecture Snapshot

| Layer | What it is |
|---|---|
| **Backend** | ASP.NET Core 10 Web API, layered solution in `api/`: `RecipeApp` (host — DI, controllers, DTOs, seed command), `RecipeApp.Core` (pure domain: models, interfaces, `IngredientText` parsing; zero infra deps), `RecipeApp.Infrastructure` (EF Core `DbContext`, repositories, migrations). |
| **Database** | EF Core 10 + **SQLite** at `data/recipes.db` (repo-relative; `SqliteDatabase.Resolve` rewrites relative paths against the repo root and forces `Foreign Keys=True`). Zero-container local dev. Migrations are the schema source of truth. |
| **Media** | Recipe photos on the **local filesystem** under `data/images/{recipeId}/{guid}{ext}`. SQLite stores only filename/metadata via the `RecipeImage` table (cascade-deleted with the recipe). Served at `/api/images/{recipeId}/{fileName}` through a `PhysicalFileProvider`, so the Vite dev proxy (`/api` → :5051) forwards images in dev exactly like production. |
| **Frontend** | SvelteKit + **Svelte 5 Runes** SPA in `client/` — `@sveltejs/adapter-static`, `fallback: 'index.html'`, `export const ssr = false`. Plain fetch via `src/lib/api.js`; fraction-aware quantity display via `src/lib/format.js` (mirrors C# `IngredientText.FormatNumber`). |
| **Ingestion** | Deterministic Python 3.12 parser `extract_recipes.py` (`Recipes.md` → `recipes_json/*.json`; stdlib only, no LLM) + a C# seeder (`dotnet run --project api/RecipeApp -- seed`, idempotent). Quantity/unit/name splitting and normalization happen in C# at seed time, not Python. |
| **Production hosting** | `./start.sh` builds `client/dist` + the Release API and runs a **single .NET process on `http://0.0.0.0:5051`** serving SPA + API + images + an installable PWA (`manifest.webmanifest` + icons). Unknown `/api/*` still 404s with JSON; unmatched non-API routes fall back to `index.html`. |

---

## 3. Completed Deliverables

- [x] **Deliverable 1: Recipe Extraction & Initial API/DB Foundation** — 76 recipes extracted from
      `Recipes.md` by the deterministic parser; layered backend scaffold, EF Core schema with
      normalized `Ingredient` + `RecipeIngredient` bridge and ordered `RecipeStep`s, full recipe
      CRUD + search/filter endpoints, seed command. (Phase-2 "usable state", 2026-09-23; predates
      the ticket-numbered reports.)
- [x] **Deliverable 2: SQLite Cutover & Ingredient Normalization Pipeline** — moved off Postgres to
      zero-container SQLite; `IngredientText.SplitLine` with unit canonicalization, fraction/range
      parsing (`5-6 eggs`, `1 or 2 bay leaves`, `3 / 4 cup`), prep-note extraction, container-unit
      promotion, and `IngredientCategoryClassifier` across 11 grocery aisles; 351 unique normalized
      ingredients. → `TICKET_1_REPORT.md`.
- [x] **Deliverable 3: Phase 4 Shopping Lists & Smart Aggregation** — `ShoppingList` +
      `ShoppingListItem`, `ShoppingListAggregator` with volume (`tsp/tbsp/cup`) and weight
      (`oz/lb`) conversions, aisle grouping, optimistic check-off, and read-only share-token links.
      → `TICKET_2_REPORT.md`.
- [x] **Deliverable 4: Culinary Fractions & React → SvelteKit (Svelte 5) Cutover** —
      `IngredientText.FormatNumber` / `format.js` snapping decimals to `1/4`, `1/3`, `1/2`, `2/3`,
      `3/4`; full Svelte 5 Runes SPA replacing React with 0 `svelte-check` warnings. →
      `TICKET_3_REPORT.md`.
- [x] **Deliverable 5: Phase 5 Weekly Meal Planner** — `MealPlan` + `MealPlanEntry` keyed by ISO
      Monday, per-day Breakfast/Lunch/Dinner/Snack slots, custom-meal support, deleted-recipe
      `CustomTitle` snapshot fallback (survives `ON DELETE SET NULL`), and recipe-multiplier summing
      on "Build Week Shopping List". → `TICKET_4_REPORT.md`.
- [x] **Deliverable 6: Recipe Photo Uploads, Single-Process Hosting & Mobile PWA** — `RecipeImage`
      table + `ImageStore` under `data/images/`, hero/thumbnail gallery UI with upload/remove,
      `start.sh` single-port hosting with explicit `UseRouting()` middleware ordering, and
      `manifest.webmanifest` + icons. → `TICKET_5_REPORT.md`.

- [x] **Deliverable 7: Multi-Store Routing (WinCo / Costco) & Pantry Staples** — `Ingredient.PreferredStore`
      + `IsPantryStaple`, `ShoppingListItem.Store` (migration `AddStoreAndPantrySupport`); seed/backfill
      pantry-staple defaults and `(winco)`/`costco`/`kirkland` store hints; list items inherit the
      ingredient's preferred store on aggregation and quick-add; `PATCH .../items/{id}` with `store` or
      `isPantryStaple` **persists back to the ingredient catalog** so future weekly lists remember it;
      `GET /api/stores`; Svelte 5 UI: store filter tabs with counts, one-tap store pills, quick-add that
      defaults to the active store tab, and a collapsible "Pantry Check — Already Have?" section. →
      `TICKET_7_REPORT.md`.

- [x] **Deliverable 8: One-Click Web Recipe URL Importer** — `POST /api/recipes/import-url` fetches
      any page and extracts its `schema.org/Recipe` JSON-LD (top-level node, arrays, `@graph`,
      string-or-array `@type`), decodes HTML entities, parses ISO 8601 durations + `recipeYield`,
      flattens `HowToStep`/`HowToSection` instructions, and runs every ingredient line through the
      same `IngredientText.SplitLine` + category/store/pantry pipeline as seed — imported
      ingredients dedupe against the catalog and inherit store preferences. Best-effort hero-photo
      download (magic-byte validated, ≤ 10 MB) into `data/images/{id}/`. Client: "Import from URL"
      modal on the home page (`ImportUrlModal.svelte`) that navigates straight to the new recipe;
      clean 400s for unfetchable URLs / pages without Recipe JSON-LD. → `TICKET_8_REPORT.md`.
- [x] **Deliverable 9: Hands-Free Cook Mode & Print Stylesheet** — "Cook Mode" toggle on
      `RecipeDetail.svelte`: Screen Wake Lock (`navigator.wakeLock`) with graceful fallback,
      re-acquire on tab visibility, release on toggle-off/destroy; larger counter-distance text;
      tap-to-cross-off ingredients (local only) and tap-to-highlight the active step. "Print"
      button + `@media print` CSS hiding nav, action rows, modals, and gallery chrome. →
      `TICKET_8_REPORT.md`.

---

## 4. Upcoming Deliverables Backlog

- [ ] **Deliverable 10: Automated Unit Tests (`RecipeApp.Tests`) & Backup Script**
  - [ ] xUnit test project for `RecipeApp.Core` + `backup.sh` archiving `data/recipes.db` and
        `data/images/`.

---

## 5. Key Decisions & Gotchas (condensed log)

Institutional knowledge preserved from the deleted `ROADMAP.md`. Detailed, binding rules live in
`AGENTS.md`; entries here explain *why*.

- **SQLite over Postgres (2026-10-16).** Zero-container local dev. The Npgsql `InitialCreate` was
  replaced by a fresh SQLite one per ticket (no dual-provider history kept); `postgres.sh` deleted.
  Startup runs `EnsureCreatedAsync` so a fresh clone works before seeding.
- **No constructor GUIDs.** All five domain models once initialized `Id = Guid.NewGuid()` in their
  constructors; EF then tracked new child entities as `Modified`, so PUT issued UPDATEs against
  non-existent rows → `DbUpdateConcurrencyException` (a real 500). Fix: EF generates keys at save
  time. Never reintroduce constructor/initializer GUIDs.
- **Deterministic extraction, no LLM.** The markdown is regular enough to parse with plain rules;
  the old `openai` dependency is gone and `requirements.txt` has been deleted (the script is
  stdlib-only). Re-run `python3 extract_recipes.py --dry-run` after any `Recipes.md` edit.
- **Normalization lives in C# at seed time** (`IngredientText.SplitLine` +
  `IngredientCategoryClassifier`), not Python — one place for domain mapping; JSON stays a raw
  import format. `NormalizedName = name.ToLowerInvariant()` (unique index) drives cross-recipe
  dedup.
- **Culinary fractions.** `IngredientText.FormatNumber` renders decimals within 0.01 of 1/8…7/8 as
  ASCII fractions (`0.6667 cup` → `2/3 cup`), mirrored client-side in `src/lib/format.js`.
- **Meal planner.** One `MealPlan` row per ISO week (unique normalized Monday). Weeks with no plan
  are served as an empty virtual plan — no row until the first entry. Repeated recipes sum on list
  build (`1x + 2x` → 3× quantities); custom meals are skipped. No drag-and-drop in v1 — moving a
  meal is a PATCH to a new day/slot (add @dnd-kit only if manual moves annoy).
- **Images on the local filesystem.** SQLite stores only `FileName`/metadata; uploads validate
  extension (`.jpg/.jpeg/.png/.webp/.gif`) and size (≤ 10 MB), stored as `{guid}{ext}`. Deleting a
  recipe removes its image dir; deleting the last photo removes the now-empty dir.
- **Explicit `app.UseRouting()` is load-bearing.** In .NET 10, without it, endpoint matching —
  including the `Map("/api/{**rest}")` catch-all — runs ahead of `UseStaticFiles`, so every
  `/api/images/...` request 404s as JSON. The static-file middleware must run before an explicit
  `UseRouting()`, which must come before `MapControllers`. (Found during TICKET_5 E2E.)
- **Dev vs prod parity.** The Vite dev proxy forwards `/api` → :5051, so images and API behave
  identically in dev and production.
