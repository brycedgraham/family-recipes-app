#!/usr/bin/env bash
#
# Family Recipes — restore the live dataset (SQLite DB + recipe images) from a backup
# archive produced by ./backup.sh (top-level entries: recipes.db, images/).
#
#   ./restore.sh <path-to-archive.tar.gz>
#
# Steps: validate the archive -> take a pre-restore safety snapshot (./backup.sh, so this
# restore itself is reversible) -> stop the container -> remove stale -wal/-shm sidecars so
# SQLite does not replay old WAL frames over the restored DB -> extract into data/ -> start.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_DIR="$REPO_ROOT/data"
DB_FILE="$DATA_DIR/recipes.db"

if [ $# -ne 1 ]; then
  echo "Usage: $0 <path-to-archive.tar.gz>" >&2
  exit 1
fi
ARCHIVE="$1"

[ -f "$ARCHIVE" ] || { echo "ERROR: archive not found: $ARCHIVE" >&2; exit 1; }

echo "==> Validating $ARCHIVE …"
if ! tar -tzf "$ARCHIVE" >/dev/null 2>&1; then
  echo "ERROR: $ARCHIVE is not a valid .tar.gz archive." >&2
  exit 1
fi
if ! tar -tzf "$ARCHIVE" | awk '/(^|\/)recipes\.db$/ { found = 1 } END { exit found ? 0 : 1 }'; then
  echo "ERROR: archive does not contain recipes.db — refusing to restore." >&2
  exit 1
fi

echo "==> Taking pre-restore safety snapshot (./backup.sh) …"
(cd "$REPO_ROOT" && ./backup.sh)

cd "$REPO_ROOT"
echo "==> Stopping container …"
docker compose stop

echo "==> Removing stale WAL sidecars and extracting into $DATA_DIR …"
rm -f "$DB_FILE-wal" "$DB_FILE-shm"
tar -xzf "$ARCHIVE" -C "$DATA_DIR"

echo "==> Starting container …"
docker compose start

echo "==> Restore complete. Verify with: curl -fsS http://localhost:5051/api/recipes | head -c 200"
