# Discovery Audit — family-recipes-app

Date: 2026-10-16 · Read-only audit; no files modified.

## 1. Workspace Structure

```
family-recipes-app/
├── api/                            # .NET 10 solution (RecipeApp.sln)
│   ├── RecipeApp/                  # Web API host
│   │   ├── Program.cs, SeedData.cs, RecipeApp.http
│   │   ├── Controllers/            # RecipesController, CategoriesController
│   │   └── Dtos/RecipeDtos.cs
│   ├── RecipeApp.Core/             # Domain (no infra deps)
│   │   ├── Models/                 # Recipe, Ingredient, RecipeIngredient, Category, RecipeStep
│   │   ├── Interfaces/IRecipeRepository.cs
│   │   └── IngredientText.cs       # qty/unit/name line splitter
│   └── RecipeApp.Infrastructure/   # EF Core layer
│       ├── Data/RecipeDbContext.cs
│       ├── Migrations/             # InitialCreate (2026-09-23) + snapshot
│       └── Repositories/RecipeRepository.cs
├── client/                         # React 19 + Vite 7 SPA
│   ├── src/                        # App.jsx, main.jsx, api.js, dataModels.js
│   │   └── components/             # CategoryList, RecipeCard, RecipeDetail, RecipeForm, RecipeList, ThemeToggle
│   └── dist/                       # committed build output
├── recipes_json/                   # 76 generated recipe JSON files (gitignored)
├── Recipes.md                      # source corpus: 76 recipes / 1485 lines
├── extract_recipes.py              # MD → JSON ingestion (deterministic, no LLM)
├── pyproject.toml · requirements.txt · .python-version (3.12)
├── postgres.sh · .env · .env.example
└── README.md (EMPTY) · PROJECT_PLAN.md (311 ln) · ROADMAP.md (69 ln)
```

**Agent rules file:** none. No `AGENTS.md`, `CLAUDE.md`, or `.cursorrules` anywhere. `ROADMAP.md:24` references a "stale" CLAUDE.md that no longer exists. `README.md` is 0 bytes — `ROADMAP.md` + `PROJECT_PLAN.md` are the de-facto project docs.

## 2. Environment & Stack

| File | Contents |
|---|---|
| `pyproject.toml` | name `family-recipes-app`, `requires-python >=3.12`, **no dependencies** |
| `requirements.txt` | `openai`, `python-dotenv` — **stale**: ROADMAP.md:19 says "the old `openai` dependency is gone"; `extract_recipes.py` imports only `re/sys/pathlib` and uses neither package |
| `client/package.json` | React 19.1, react-router-dom 7.6, Vite 7, ESLint 9 (flat config); scripts dev/build/lint/preview |
| `.env.example` | `DB_USER/PASSWORD/NAME=recipeapp/devpassword/recipeapp`; LLM vars (`LLM_BASE_URL=http://127.0.0.1:8888/v1`) — also vestigial since extraction is now deterministic |

Stack in practice: **ASP.NET Core 10 Web API + EF Core (Npgsql) + PostgreSQL 16 (Docker, port 5432)** ↔ **React 19/Vite** (dev proxy `/api` → `http://localhost:5051`, per `client/vite.config.js` and `launchSettings.json`). Python is only the one-off ingestion script.

## 3. Database Schema & Data

**There is no SQLite file anywhere in the repo** (searched all depths, incl. hidden) — so `sqlite3 .schema` was not applicable. The database is **PostgreSQL**; exact DDL comes from the EF Core migration `20260923065928_InitialCreate.cs`:

```sql
-- 6 tables; uuid PKs throughout
Categories(Id uuid PK, Name varchar(100) UNIQUE NOT NULL, Description varchar(500))
Ingredients(Id uuid PK, Name varchar(200) NOT NULL, NormalizedName varchar(200) UNIQUE NOT NULL, Category varchar(50))
Recipes(Id uuid PK, Title varchar(200) NOT NULL [indexed], Description varchar(2000), Cuisine varchar(100),
        Servings int?, PrepTimeMinutes int?, CookTimeMinutes int?, CreatedAt/UpdatedAt timestamptz)
RecipeIngredients(Id uuid PK, RecipeId uuid → Recipes CASCADE, IngredientId uuid → Ingredients RESTRICT,
        Quantity numeric(18,4)?, Unit varchar(20)?, Notes varchar(200)?)   -- (RecipeId,IngredientId) UNIQUE
RecipeSteps(Id uuid PK, RecipeId uuid → Recipes CASCADE, StepNumber int, Text varchar(2000))  -- (RecipeId,StepNumber) UNIQUE
RecipeCategories(CategoriesId uuid, RecipesId uuid, composite PK)          -- M2M, cascade both sides
```

**Live data: unavailable.** The `recipeapp-postgres` container is not running and no stopped container or `recipeapp_postgres_data` volume exists on this machine, so row counts/samples can't be queried. `ROADMAP.md` claims all **76 recipes were seeded 2026-09-23**, but that DB state is not present here (re-seed = `bash postgres.sh && dotnet run -- seed`).

**Normalization as currently implemented** — this is the key structure to know:

- Splitting happens in C# at seed time (`SeedData.cs` + `IngredientText.SplitLine`), not Python. Regex: leading number (mixed/simple fraction, decimal, integer; Unicode ½⅓¼¾… normalized first) → `Quantity`; first alphabetic word after it → `Unit`; remainder → ingredient `Name`.
- **The only normalization is `NormalizedName = name.ToLowerInvariant()`** (`RecipeRepository.FindOrCreateIngredientAsync`). No unit synonyms ("tsp" vs "teaspoon" are distinct), no quantity conversion, no category population (`Ingredient.Category` always null from seed — flagged as a known gap in ROADMAP.md:68).
- **`Notes` is never populated**: the seeder sets only `Quantity`/`Unit`/`IngredientId`; semicoloned qualifiers stay glued to the name.

Sample mapping (from `recipes_json/bourbon-chicken.json`, raw → stored):

| Raw line | Quantity | Unit | Name (→ NormalizedName) |
|---|---|---|---|
| `2 pounds boneless chicken breasts; cut into bite-size pieces` | 2 | pounds | `boneless chicken breasts; cut into bite-size pieces` |
| `1/4 teaspoon ginger` | 0.25 | teaspoon | `ginger` |
| `1 tablespoon olive oil; (1-2)` | 1 | tablespoon | `olive oil; (1-2)` |

Second sample, edge case (`mac-and-cheese-variations.json`): `"ingredients": []` — notes-only entry, intentional per ROADMAP.md:12. The API DTO flattens this back to the client shape `{name, quantity:"0.25 teaspoon"}` via `IngredientText.FormatQuantity` (`Dtos/RecipeDtos.cs`).

## 4. Current Implementation (per file)

**Python (ingestion)**
- `extract_recipes.py` — deterministic parser: splits `Recipes.md` on `#`/`##` headers, classifies lines as ingredients vs instructions (quantity-leading or short label → ingredient; sentence-like with terminal punctuation → instruction), handles explicit `Ingredients:`/`Instructions:`/`Directions:` headers, unescapes markdown, writes one JSON per recipe into `recipes_json/`. Supports `--dry-run`. 188 lines.

**C# backend**
- `RecipeApp.Core/Models/*` — 5 POCOs; no constructor GUIDs (removed to fix a PUT→500 bug, ROADMAP.md:18).
- `RecipeApp.Core/IngredientText.cs` — regex splitter/formatter for ingredient lines (incl. Unicode fractions); also used by the API mapper for display strings.
- `RecipeApp.Core/Interfaces/IRecipeRepository.cs` — repo contract + `CategorySummary` record.
- `RecipeApp.Infrastructure/Data/RecipeDbContext.cs` — EF config: unique indexes (`NormalizedName`, category name, recipe step ordering), cascade/restrict FKs, M2M entity.
- `RecipeApp.Infrastructure/Migrations/*` — single `InitialCreate` migration (the DDL above).
- `RecipeApp.Infrastructure/Repositories/RecipeRepository.cs` — includes-based queries, search by `q` (title/description LIKE) + category filter, CRUD, find-or-create for categories/ingredients.
- `RecipeApp/Program.cs` — DI wiring; **dual mode**: `dotnet run -- seed` runs the importer and exits, otherwise hosts the API (OpenAPI in Development).
- `RecipeApp/SeedData.cs` — idempotent import of `recipes_json/*.json` (skips existing titles), splits lines, find-or-creates ingredients/categories, numbers steps.
- `Controllers/RecipesController.cs` — full CRUD on `/api/recipes` (`?q=`, `?category=`); PUT rebuilds child collections.
- `Controllers/CategoriesController.cs` — `GET /api/categories` with recipe counts.
- `Dtos/RecipeDtos.cs` — request/response records mirroring the client's data model; `ImageUrls` hardcoded to `[]` (Phase 6).
- `RecipeApp.http` — **stale**: still the default `weatherforecast` template, no actual endpoint examples.

**React frontend**
- `main.jsx` / `App.jsx` — router shell: home layout (category sidebar + debounced search + recipe grid), detail page with delete, add/edit form routes.
- `api.js` — thin fetch layer against `/api` (list/detail/categories/create/update/delete).
- `dataModels.js` — JSDoc typedefs for Recipe/Ingredient/Category + example objects (no runtime logic).
- Components: `CategoryList` (sidebar, "All Recipes"), `RecipeCard` (grid card w/ image slot), `RecipeList`, `RecipeDetail` (ingredients as "qty name", ordered steps), `RecipeForm` (create/edit, dynamic ingredient rows, comma-separated categories), `ThemeToggle` (light/dark, localStorage + media query).
- No mock data remains — all wired to the real API.

**Ops/docs**
- `postgres.sh` — raw `docker run` of postgres:16 from `.env`; no compose file, no test project, no CI (all listed as known gaps in ROADMAP.md:65-69).

## Notable observations

1. **No SQLite** — the audit premise doesn't match reality; everything is Postgres + EF Core, and the DB isn't currently up on this machine.
2. `requirements.txt` and `.env.example` LLM vars are dead weight from the abandoned LLM extraction approach.
3. Ingredient normalization is minimal (lowercase only) — ROADMAP's Phase 4 (shopping lists) explicitly depends on improving it; `Notes` column exists but is never written.
4. No agent rules file to conform to yet; `README.md` empty; `RecipeApp.http` stale.
