#!/usr/bin/env bash
# Single-process production launcher for the Family Recipes app.
#
# Builds the SvelteKit SPA (client/dist) and then serves the entire app —
# UI, REST API, SQLite database, and recipe images — from one .NET process
# listening on http://0.0.0.0:5051 so it is reachable from other devices on
# the local network (e.g. phones installing the PWA).
#
# Usage: ./start.sh
set -euo pipefail

cd "$(dirname "$0")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:${PATH}"

echo "==> Building SvelteKit frontend (client/dist)..."
if [ ! -d client/node_modules ]; then
  npm --prefix client install
fi
npm --prefix client run build

echo "==> Building .NET API (Release)..."
dotnet build api/RecipeApp.sln -c Release

APP_DIR="api/RecipeApp/bin/Release/net10.0"
if [ ! -f "$APP_DIR/RecipeApp.dll" ]; then
  echo "ERROR: expected built host at $APP_DIR/RecipeApp.dll (run dotnet build first?)" >&2
  exit 1
fi

echo "==> Starting single-process server on http://0.0.0.0:5051 (Ctrl+C to stop)"
# Run from the app's base directory so WebApplication.CreateBuilder finds appsettings.json there;
# SqliteDatabase/SPA paths resolve against the repo root regardless of the working directory.
cd "$APP_DIR"
exec dotnet RecipeApp.dll --urls http://0.0.0.0:5051
