# Family Recipes App

A family recipe management web app: browse/search/edit recipes, plan a weekly meal grid, build
aggregated shopping lists (with aisle grouping and shareable read-only links), and upload recipe
photos — all served from a single process on your local network as an installable PWA.

**Stack:** ASP.NET Core 10 + EF Core + SQLite · SvelteKit (Svelte 5) SPA · deterministic Python
ingestion. No containers, no external services.

## Production launch (one command)

```bash
./start.sh
```

Builds the static SPA (`client/dist`) and the Release API, then runs one .NET process serving the
UI, API, SQLite database, and recipe photos at **`http://0.0.0.0:5051`** — reachable from any
device on your network (phones included). Open it in a mobile browser to install the app
(`manifest.webmanifest`).

## Development workflow

```bash
# 1. Backend — API at http://localhost:5051
dotnet run --project api/RecipeApp

# 2. Frontend — Vite dev server, proxies /api → :5051
npm --prefix client install        # first time only
npm --prefix client run dev

# Checks (keep both green)
dotnet build api/RecipeApp.sln
npm --prefix client run check      # svelte-check: 0 errors / 0 warnings
```

## Ingestion & seeding

Recipe content lives in `Recipes.md` (source of truth). `recipes_json/` is generated — never edit
it by hand.

```bash
python3 extract_recipes.py --dry-run   # preview the parse after editing Recipes.md
python3 extract_recipes.py             # regenerate recipes_json/*.json
dotnet run --project api/RecipeApp -- seed   # idempotent import (skips existing titles)
```

## Documentation

- **`PLAN.md`** — the single source of truth: architecture snapshot, completed deliverables, and
  the upcoming backlog. Start here (and at its cold-start handoff protocol) to resume work.
- **`AGENTS.md`** — binding conventions for contributors and AI agents: layer rules, Svelte 5 runes
  rules, commands, and load-bearing gotchas.
- **`TICKET_N_REPORT.md`** — per-deliverable change reports (what changed, API surface,
  verification evidence).
