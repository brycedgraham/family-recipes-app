# ROADMAP_CHECK — Phase/Goal Summary + Source Extracts

Prepared 2026-10-16 for the Senior Architect to write the SQLite migration + ingredient normalization work order. Section 3 contains verbatim copies of `api/RecipeApp.Core/IngredientText.cs` and `api/RecipeApp/Program.cs`.

## 1. ROADMAP.md — living status (as of 2026-09-23)

**Goal achieved:** Phase 2 milestone "usable state" — all 76 recipes from `Recipes.md` loaded into Postgres, browsable/searchable/editable through the React UI against the real API. Verified via curl + browser E2E incl. SQL-level edit persistence.

### What exists
- **Data (complete):** 76 recipes in 7 categories; deterministic extraction by `extract_recipes.py` (no LLM); every recipe has ingredients + instructions except `Mac and Cheese Variations` (notes-only source entry, intentional).
- **Backend:** layered solution (Core / Infrastructure / host), EF Core schema with normalized `Ingredient` + `RecipeIngredient` bridge, ordered `RecipeStep`s, category M2M. `Quantity` nullable (many lines have no quantity).
- **Frontend:** React 19 + Vite + router; list/detail/form/category components fully wired to the API (mock data deleted).

### Decisions log
- **Postgres via Docker** (`postgres.sh`, port 5432) — plan's SQL Server option dropped. *(Note: current direction is moving off this to SQLite for zero-container local simplicity — see AGENTS.md.)*
- **Bug fix (2026-09-23): recipe edit returned 500.** Models initialized `Id = Guid.NewGuid()` in constructors → EF tracked new children as `Modified`, PUT issued UPDATEs against non-existent rows → `DbUpdateConcurrencyException`. Fix: removed constructor GUID initializers from all five models.
- **Deterministic extraction, no LLM** — the old `openai` dependency is gone (though `requirements.txt` still lists it).
- **Parser rules (`extract_recipes.py`):** quantity-starting lines and short colon-terminated labels before any step count as ingredients; sentence-like lines (terminal punctuation, no quantity) count as instructions. Literal `Ingredients`/`Instructions`/`Directions` headers split the section when present. Rules are tuned to this corpus — re-run with `--dry-run` after any `Recipes.md` edit.
- **Quantity/unit/notes splitting happens in the C# seeder**, not Python — domain mapping stays in one place; JSON remains a raw import format.
- **No React Query yet** — plain fetch + hooks for v1.
- **Dev server proxy** (`vite.config.js` → `/api`) instead of CORS.
- **Environment:** plain Linux (not WSL2); .NET 10 SDK installed to `~/.dotnet` via dotnet-install.sh; use `$HOME/.dotnet/dotnet`. *(Verified 2026-10-16: dotnet is not currently present on this machine — reinstall before building.)*

### Phase 2 checklist
All items checked: data pipeline (audit, rewrite extractor, extract/validate all 76), API (nullable quantity, `InitialCreate` migration, full CRUD + search/filter endpoints, seed command, build passes), client (proxy, fetch layer, list/detail/form against API), verification (seed, curl round-trips, browser E2E incl. edit persistence, commit).

### Remaining roadmap
| Phase | Summary | Notes |
|---|---|---|
| 3 · Auth/users | ASP.NET Identity + JWT, family invite flow, per-user ownership | Needs `UserId` FK on Recipes — schema change |
| 4 · Shopping lists | Generate from recipes, dedupe ingredients, shareable read-only URL | Depends on ingredient normalization quality (seed splitting) |
| 5 · Meal planning | Weekly grid, drag-and-drop, week → shopping list | @dnd-kit |
| 6 · Polish/scale | Photos, PWA, ratings/notes, import from URL, FTS, backups, E2E tests | Prioritize by family usage |

### Known gaps / follow-ups
- No test project yet (plan called for `RecipeApp.Tests`) — add when endpoints stabilize.
- No CI/CD, no docker-compose file (raw `docker run` in `postgres.sh`).
- `Ingredient.Category` (produce/dairy/pantry) not populated by seed — needs a word list or LLM pass later; blocks Phase 4 grouping.
- Recipe metadata (servings, prep/cook time, cuisine) absent from source markdown — null until entered via UI.

## 2. PROJECT_PLAN.md — full plan summary

**Stack (planned):** ASP.NET Core 10 Web API · React + Vite (TanStack Query planned) · SQL Server or PostgreSQL via EF Core code-first migrations · repository pattern · Identity + JWT auth · FluentValidation (backend) / Zod (frontend) · React Hook Form · @dnd-kit · Docker + docker-compose · GitHub Actions. *(Actuals to date: Postgres, plain fetch, no validation libs, no CI — see ROADMAP.md.)*

**Architecture:** `RecipeApp.Api` (routing/controllers/middleware), `RecipeApp.Core` (domain, zero infra deps), `RecipeApp.Infrastructure` (EF Core repositories, DI-registered in Api), `RecipeApp.Tests` (xUnit + in-memory EF), `client/` (components/pages/hooks/api/lib).

### Phases
1. **Foundation** (1–2 wks): solution/projects scaffold, git hygiene, docker-compose dev parity, CI/CD, initial schema design (`Recipes`, `Ingredients`, `RecipeIngredients`, `Tags`/`RecipeTags`; later `Users`/`ShoppingLists`/`MealPlans`), first EF migration, API + React skeletons (OpenAPI, problem details, CORS; TanStack Query, router, component library), xUnit + Vitest.
2. **Core Recipe App** (2–3 wks) ✅ *done per ROADMAP.md*: markdown import spec + parser (plan said C# Markdig; actual: Python deterministic extractor + C# seeder split), seed command, repository/service CRUD endpoints (`GET /recipes` with `?q=`/`?tags=`/`?cuisine=`/`?maxTime=`, by-id, POST/PUT/DELETE, FluentValidation planned), React list/detail (servings scaler, print CSS)/form (dynamic ingredient rows, RHF+Zod) / search+filter sidebar with URL-synced state.
3. **Users & Auth** (1–2 wks): Identity + EF store, `ApplicationUser` with family fields; JWT 15-min access + DB refresh tokens (7–30 d); register/login/refresh/logout endpoints; Admin/Member policies; `UserId` FK on Recipes; frontend auth pages, context, `PrivateRoute`, Axios interceptor auto-refresh; profile/settings page; recommended family invite-link system.
4. **Shopping Lists** (1–2 wks): `ShoppingLists`/`ShoppingListItems` schema; aggregation service (recipe IDs + serving multipliers → deduped merged list, unit conversions for common cases); generate endpoint + full CRUD + item PATCH + read-only share token URL; frontend "Add to list" with servings selector, list page with check-off/strikethrough/category grouping/manual items, shareable no-login URL (grocery-store mobile use).
5. **Meal Planning** (2 wks): `MealPlans`/`MealPlanEntries` (dayOfWeek 0–6, mealType enum, servings); week-based endpoints; `POST /meal-plans/{id}/shopping-list` reusing Phase 4 aggregation; weekly 7-column grid with @dnd-kit drag-and-drop + click-to-search fallback; one-click week → shopping list; stretch: nutrition summary (USDA FoodData Central).
6. **Polish & Scale** (ongoing): photo uploads (plan said Azure Blob/S3 — current decision per AGENTS.md is local filesystem `data/images/` + static serving), PWA via Vite plugin, per-user ratings/notes, import from URL (schema.org JSON-LD via HtmlAgilityPack), FTS upgrade (SQL full-text / Meilisearch / Postgres tsvector — revisit for SQLite), automated backups with tested restore, monitoring (App Insights/Sentry) + 5xx alerts, Playwright E2E in CI.

**Key milestones:** recipes visible in browser (P2) · search/filter end-to-end (P2) · family accounts + ownership (P3) · one-click shopping list (P4) · shareable list URL on mobile (P4) · DnD weekly planner (P5) · week → shopping list (P5).

**Plan notes relevant to the upcoming work:**
- Store ingredients as structured data (name/quantity/unit separate columns) from day one — this is what makes Phase 4 aggregation work. *(Already true; normalization quality is the open item.)*
- Invest upfront in a strict markdown parsing spec + tests against real examples.
- Search: start with SQL LIKE; only add a dedicated index after ~200+ recipes.

## 3. Verbatim source extracts

### 3.1 `api/RecipeApp.Core/IngredientText.cs` (complete, 103 lines)

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace RecipeApp.Core;

/// <summary>
/// Splits raw ingredient text into structured parts.
/// "1/2 package (8 ounces) bow tie pasta" -> (0.5, "package", "(8 ounces) bow tie pasta").
/// Lines without a leading number have no quantity or unit.
/// </summary>
public static partial class IngredientText
{
    // Leading number: mixed fraction ("1 1/2"), simple fraction ("3/4"), decimal ("0.5"), or integer ("2").
    [GeneratedRegex(@"^(?:(\d+)\s+)?(\d+)/(\d+)|^(\d+(?:\.\d+)?)")]
    private static partial Regex QuantityRegex();

    // First alphabetic word after the number: "cup", "tbsp", "package".
    [GeneratedRegex(@"^[A-Za-z][A-Za-z/-]*")]
    private static partial Regex UnitRegex();

    // Unicode fraction characters found in the seed data ("½ cup Butter").
    private static readonly Dictionary<char, decimal> UnicodeFractions = new()
    {
        ['½'] = 0.5m, ['¼'] = 0.25m, ['¾'] = 0.75m,
        ['⅓'] = 1m / 3m, ['⅔'] = 2m / 3m,
        ['⅛'] = 0.125m, ['⅜'] = 0.375m,
        ['⅕'] = 0.2m, ['⅖'] = 0.4m, ['⅗'] = 0.6m, ['⅘'] = 0.8m,
        ['⅙'] = 1m / 6m, ['⅚'] = 5m / 6m,
    };

    /// <summary>
    /// Rewrites a leading unicode fraction (optionally after a whole number, "1½") as a decimal ("1.5").
    /// </summary>
    private static string NormalizeLeadingFraction(string text)
    {
        var i = 0;
        while (i < text.Length && char.IsDigit(text[i])) i++;
        if (i < text.Length && UnicodeFractions.TryGetValue(text[i], out var fraction))
        {
            var whole = i > 0 ? text[..i] : string.Empty;
            return whole + fraction.ToString("0.####", CultureInfo.InvariantCulture) + text[(i + 1)..];
        }
        return text;
    }

    /// <summary>
    /// Splits a raw ingredient line into quantity, unit, and remaining name text.
    /// </summary>
    public static (decimal? Quantity, string? Unit, string Name) SplitLine(string? raw)
    {
        var text = NormalizeLeadingFraction((raw ?? string.Empty).Trim());
        if (text.Length == 0) return (null, null, string.Empty);

        var match = QuantityRegex().Match(text);
        decimal? quantity;
        if (match.Groups[2].Success) // fraction form
        {
            var denominator = int.Parse(match.Groups[3].Value);
            if (denominator == 0) return (null, null, text);
            var whole = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            quantity = whole + (decimal)int.Parse(match.Groups[2].Value) / denominator;
        }
        else if (match.Groups[4].Success) // plain number
        {
            quantity = decimal.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            return (null, null, text);
        }

        var rest = text[match.Length..].TrimStart();
        string? unit = null;
        var unitMatch = UnitRegex().Match(rest);
        if (unitMatch.Success)
        {
            unit = unitMatch.Value;
            rest = rest[unitMatch.Length..].TrimStart();
        }

        return (Math.Round(quantity.Value, 4), unit, rest);
    }

    /// <summary>
    /// Parses a quantity string ("1/2 cup") into a number and unit word.
    /// </summary>
    public static (decimal? Quantity, string? Unit) ParseQuantity(string? text)
    {
        var (quantity, unit, _) = SplitLine(text);
        return (quantity, unit);
    }

    /// <summary>
    /// Formats quantity + unit back into a display string ("0.5 cup"); null when neither is present.
    /// </summary>
    public static string? FormatQuantity(decimal? quantity, string? unit)
    {
        if (quantity == null && string.IsNullOrEmpty(unit)) return null;
        var number = quantity?.ToString("0.####", CultureInfo.InvariantCulture);
        var suffix = unit is null ? string.Empty : " " + unit;
        return (number + suffix).Trim();
    }
}
```

### 3.2 `api/RecipeApp/Program.cs` (complete, 41 lines)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RecipeApp.Core.Interfaces;
using RecipeApp.Infrastructure.Data;
using RecipeApp.Infrastructure.Repositories;
using RecipeApp.Api;

// `dotnet run -- seed` imports recipes_json/*.json and exits.
if (args.Contains("seed"))
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();
    await SeedData.RunAsync(configuration);
    return;
}
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Database
builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
```

### 3.3 Context notes for the work order

- Provider switch point: `Program.cs` line `options.UseNpgsql(...)` (both API host and `SeedData.RunAsync`, which builds its own `DbContextOptionsBuilder().UseNpgsql(connectionString)`) — both call sites must move to `UseSqlite`.
- Connection string today (`appsettings.json`): `Host=localhost;Port=5432;Database=recipeapp;Username=recipeapp;Password=devpassword`.
- Existing schema (single migration `InitialCreate`): 6 tables — `Recipes`, `Ingredients` (unique `NormalizedName`), `RecipeIngredients` (numeric(18,4) quantity, unique `(RecipeId, IngredientId)`), `RecipeSteps` (unique `(RecipeId, StepNumber)`), `Categories` (unique name), `RecipeCategories` M2M.
- Seed path: `SeedData.RunAsync` → `EnsureCreatedAsync()` + per-file idempotent import; `FindOrCreateIngredientAsync` normalizes via `ToLowerInvariant()` only.
- Environment caveat: `dotnet` is not currently installed on this machine (verified 2026-10-16); install .NET 10 SDK before build/seed steps.
