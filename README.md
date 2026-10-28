# Family Recipes App

A family recipe management web app: browse/search/edit recipes, plan a weekly meal grid, build
aggregated shopping lists (with aisle grouping and shareable read-only links), and upload recipe
photos — all served from a single process on your local network as an installable PWA.

**Stack:** ASP.NET Core 10 + EF Core + SQLite · SvelteKit (Svelte 5) SPA · deterministic Python
ingestion. Optionally deployable as a single Docker container with auto-start on boot.

## Production launch (one command)

```bash
./start.sh
```

Builds the static SPA (`client/dist`) and the Release API, then runs one .NET process serving the
UI, API, SQLite database, and recipe photos at **`http://0.0.0.0:5051`** — reachable from any
device on your network (phones included). Open it in a mobile browser to install the app
(`manifest.webmanifest`).

## Docker (auto-start on boot)

```bash
docker compose up -d --build     # build image, start at http://0.0.0.0:5051
docker compose logs -f           # follow logs
docker compose down              # stop (data stays in ./data)
```

- **Auto-start:** `docker.service` starts at boot and the service's `restart: unless-stopped`
  policy brings the container back up with it (and after crashes). It only stays down if you
  explicitly stop it.
- **Data:** `./data` is bind-mounted to `/data` — the same SQLite DB + recipe images the native
  `./start.sh` uses, so both deployment modes share one dataset. On first boot with an empty
  volume the container seeds itself from `recipes_json/`.
- **Re-seed manually** (e.g. after regenerating `recipes_json/` and rebuilding):
  `docker compose run --rm app dotnet RecipeApp.dll seed` — idempotent, skips existing titles.

## Backup & restore

Three layers, all targeting the same live data (`./data` — bind-mounted to `/data` in Docker):

```bash
# 1. One-click off-site copy from the browser:
#    sidebar → "Download Backup" → GET /api/backup streams recipeapp-backup-YYYY-MM-DD.zip
#    (a consistent SQLite snapshot via VACUUM INTO + everything under data/images/)

# 2. Local archive backup — WAL-safe online hot backup, retains the newest 14:
./backup.sh
./backup.sh --install-cron   # nightly at 03:00 (idempotent), logs to backups/backup.log

# 3. Restore from an archive — takes a pre-restore safety snapshot first,
#    stops the container, clears stale -wal/-shm, extracts, starts again:
./restore.sh backups/recipeapp_backup_YYYYMMDD_HHMMSS.tar.gz
```

- The live DB runs in **WAL mode**, so `backup.sh` uses SQLite's online backup API (Python stdlib —
  no `sqlite3` CLI needed) instead of a plain file copy; the archived `recipes.db` is converted to
  rollback journal mode and is a single self-contained file. `backups/` is gitignored.
- Restore refuses archives that don't contain `recipes.db`, and always snapshots first, so a botched
  restore is itself reversible (`./restore.sh backups/<the-newer-archive>`).

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
