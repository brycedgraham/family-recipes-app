#!/usr/bin/env bash
#
# Family Recipes — offline backup of the live dataset (SQLite DB + recipe images).
#
#   ./backup.sh                 take a backup now  -> backups/recipeapp_backup_<ts>.tar.gz
#   ./backup.sh --install-cron  idempotently install a nightly 03:00 user cron job
#
# The live database runs in WAL mode, so a plain file copy would miss uncommitted WAL
# frames. Instead we use SQLite's online backup API (Python stdlib `sqlite3` — no sqlite3
# CLI required) to produce an atomic, consistent hot snapshot while the app keeps serving.
# The snapshot is then switched to rollback journal mode so the archived recipes.db is a
# single self-contained file with no -wal/-shm sidecars. Images are copied as-is.
# Only the newest 14 archives are retained.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_DIR="$REPO_ROOT/data"
BACKUP_DIR="$REPO_ROOT/backups"
DB_FILE="$DATA_DIR/recipes.db"
RETENTION=14
CRON_MARKER="# family-recipes-backup"

log() { printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"; }

install_cron() {
  local job="0 3 * * * cd $REPO_ROOT && ./backup.sh >> $BACKUP_DIR/backup.log 2>&1 $CRON_MARKER"
  local current existing
  current="$(crontab -l 2>/dev/null || true)"
  if [ -n "$current" ]; then
    existing="$(printf '%s\n' "$current" | grep -vF "$CRON_MARKER" || true)"
  else
    existing=""
  fi
  {
    [ -n "$existing" ] && printf '%s\n' "$existing"
    printf '%s\n' "$job"
  } | crontab -
  log "Installed nightly cron job (03:00): $job"
}

if [ "${1:-}" = "--install-cron" ]; then
  install_cron
  exit 0
fi

[ -f "$DB_FILE" ] || { log "ERROR: database not found at $DB_FILE"; exit 1; }
mkdir -p "$BACKUP_DIR"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
STAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/recipeapp_backup.XXXXXX")"
trap 'rm -rf "$STAGE_DIR"' EXIT

log "Snapshotting $DB_FILE (online SQLite hot backup)…"
python3 - "$DB_FILE" "$STAGE_DIR/recipes.db" <<'PYEOF'
import sqlite3, sys

src_path, dst_path = sys.argv[1], sys.argv[2]
src = sqlite3.connect(src_path)
dst = sqlite3.connect(dst_path)
try:
    with dst:
        src.backup(dst)  # atomic online hot backup; WAL-safe, no app lock
finally:
    # The live DB is in WAL mode and the snapshot inherits it. Switch to rollback
    # journal mode so any WAL frames are checkpointed into the main file, leaving a
    # single self-contained recipes.db for the archive.
    dst.execute("PRAGMA journal_mode=DELETE")
    src.close()
    dst.close()
PYEOF

entries=(recipes.db)
if [ -d "$DATA_DIR/images" ]; then
  log "Staging images…"
  cp -a "$DATA_DIR/images" "$STAGE_DIR/images"
  entries+=(images)
fi

ARCHIVE="$BACKUP_DIR/recipeapp_backup_${TIMESTAMP}.tar.gz"
log "Writing $ARCHIVE …"
# Write to a temp name and rename into place so a concurrent reader never sees a partial archive.
tar -czf "$ARCHIVE.tmp" -C "$STAGE_DIR" "${entries[@]}"
mv "$ARCHIVE.tmp" "$ARCHIVE"

mapfile -t old < <(ls -1t "$BACKUP_DIR"/recipeapp_backup_*.tar.gz 2>/dev/null | tail -n +$((RETENTION + 1)))
for f in "${old[@]}"; do
  rm -- "$f"
  log "Pruned old backup: $f"
done

log "Backup complete: $ARCHIVE"
