# TICKET_13 Report — Docker-Grade Backup & Restore (`backup.sh` / `restore.sh` / nightly cron / one-click download)

**Date:** 2026-10-25 · **Status:** DONE — build clean, `svelte-check` 0/0, deployed via Docker; backup, restore, and `/api/backup` all E2E-verified against the live container.

## 1. What changed

| File | Change |
|---|---|
| `backup.sh` | New (the repo had no backup script yet). WAL-safe **online hot backup** via Python stdlib `sqlite3.Connection.backup()` (no `sqlite3` CLI on the host — same atomic API SQLite exposes natively), staged in a temp dir; snapshot switched to rollback journal mode so the archived `recipes.db` is one self-contained file (no `-wal`/`-shm` sidecars); archives `recipes.db` + `images/` into `backups/recipeapp_backup_<ts>.tar.gz` (atomic: temp name → rename); retains the newest **14**; `--install-cron` idempotently installs `0 3 * * * cd <repo> && ./backup.sh >> backups/backup.log 2>&1 # family-recipes-backup` (marker-based replace, safe to re-run) |
| `restore.sh` | New — validates the archive is a readable `.tar.gz` containing `recipes.db` (refuses otherwise); runs `./backup.sh` first as a **pre-restore safety snapshot**; `docker compose stop`; deletes stale `-wal`/`-shm` so SQLite can't replay old WAL frames over the restored DB; extracts into `data/`; `docker compose start` |
| `.gitignore` | Added `backups/` |
| `api/RecipeApp/Controllers/BackupController.cs` | New — `GET /api/backup`: resolves the live DB path via `SqliteDatabase.Resolve`, takes a consistent standalone snapshot with `VACUUM INTO '<temp>'` (no lock, no app stop), builds `recipeapp-backup-YYYY-MM-DD.zip` (top-level `recipes.db` + `images/{recipeId}/{fileName}` for every stored photo) to a temp file, then async-streams it (`Content-Type: application/zip`, attachment disposition); both temp files deleted in `finally` |
| `client/src/routes/+page.svelte` | Bottom of `.App-sidebar`: `<a href="/api/backup" download class="secondary">Download Backup</a>` — full-width plank style matching "Import from URL"; one tap downloads the off-site `.zip` to phone or PC |

## 2. Audit: Docker volume persistence (Step 1)

- `docker-compose.yml` bind-mounts `./data:/data` and forces `ConnectionStrings__DefaultConnection=Data Source=/data/recipes.db`; the Dockerfile bakes only immutable assets (SPA at `/client/dist`, seed JSON) into the image — **all mutable state (DB + images) lives on that one bind mount**.
- `docker compose up -d --build` recreates the container, never the bind mount: data survives every rebuild (verified: 77 recipes live before and after all three rebuilds in this ticket). No named volumes are used, so even `down -v` couldn't wipe anything.
- `backup.sh`/`restore.sh` therefore target exactly `<repo_root>/data` — the host side of `/data`.

## 3. API contract

- `GET /api/backup` → 200, `Content-Type: application/zip`, `Content-Disposition: attachment; filename=recipeapp-backup-YYYY-MM-DD.zip`. Body: zip with top-level `recipes.db` (consistent standalone snapshot) and `images/{recipeId}/{fileName}`. No auth — consistent with the rest of the LAN app.

## 4. Verification (Docker, live container on :5051)

| Check | Result |
|---|---|
| `dotnet build api/RecipeApp.sln` | 0 errors ✓ (NU package-audit warnings pre-existing, untouched by this ticket) |
| `npm --prefix client run check` | 0 errors / 0 warnings ✓ |
| `npm --prefix client run build` | OK; "Download Backup" + `/api/backup` present in the production bundle ✓ |
| `docker compose up -d --build` | container recreated, data intact (77 recipes) ✓ |
| `./backup.sh` | wrote `backups/recipeapp_backup_20261025_182428.tar.gz` — top-level `recipes.db` + 1 image ✓ |
| `./backup.sh --install-cron` ×4 total runs | exit 0 every time; `crontab -l` shows exactly **one** `0 3 * * * … # family-recipes-backup` line ✓ (idempotent) |
| Retention (15 backdated dummy archives + 4 real) | pruned the 5 oldest, kept exactly 14 ✓ (dummies removed after test) |
| `curl -f -sS -o /tmp/test_backup.zip http://localhost:5051/api/backup` | 200, ~191 KB; headers `application/zip` + attachment filename ✓ |
| Zip validation (Python `zipfile` + `sqlite3`) | `testzip()` clean; entries `recipes.db` + `images/{id}/{guid}.jpg`; `PRAGMA integrity_check` = ok; **77 recipes**; `Stores` table present (WinCo, Costco); all core tables present; JPEG magic bytes `ffd8…` ✓ |
| Temp-file cleanup | no `recipeapp_backup_*` leftovers in container `/tmp` after the request ✓ |
| `./restore.sh <archive>` E2E | validate → pre-restore snapshot → stop → `-wal`/`-shm` removed → extract → start; post-restore `GET /api/recipes` = 77 ✓ |

Note on recipe count: the work order expected 76 (Deliverable 1's seed); the live DB has **77** — one extra recipe was added later via the TICKET_8 URL importer. Verified against the live DB, not the stale number. `/tmp/test_backup.zip` deleted after validation.

## 5. Gotchas found along the way

1. **No `sqlite3` CLI on the host.** The hot backup goes through Python stdlib `sqlite3.Connection.backup()` — the same atomic online-backup API SQLite exposes natively; zero new dependencies.
2. **WAL mode makes plain copies unsafe.** The snapshot inherits WAL mode from the source, so `backup.sh` switches it to rollback journal mode before archiving (checkpointing any WAL frames into the main file). On restore, `-wal`/`-shm` must be deleted first or SQLite replays stale frames over the restored DB.
3. **`ZipArchive` over Kestrel's response stream fails — two real bugs hit in E2E.** (a) An entry stream left open → `IOException: Entries cannot be created while previously created entries are still open`. (b) `ZipArchive`/`DeflateStream` flush *synchronously* on dispose → `InvalidOperationException: Synchronous operations are disallowed` (client sees curl error 18, truncated transfer). Fix: build the zip to a temp file, then async-stream that file into `Response.Body`.
4. **`pipefail` + `grep -q` SIGPIPE.** `tar -tzf x | grep -q pattern` returns 141 when the match is early (grep exits before tar finishes writing); under `set -euo pipefail` the "contains recipes.db" check falsely failed on a valid archive. Replaced with an `awk '… END { exit found ? 0 : 1 }'` that consumes all input.
5. **`set -e` + command substitution.** `existing="$(printf … | grep -vF …)"` aborts the script when grep filters *everything* out (exit 1) — which is exactly the idempotent-reinstall case where the crontab holds only our marker line. Append `|| true`.
6. **`ControllerBase.File` shadows `System.IO.File`** inside controllers — even a `using File = System.IO.File;` alias loses to member lookup (CS0119). Fully qualify `System.IO.File.*`.
