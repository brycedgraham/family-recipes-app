#!/bin/sh
# Container entrypoint: seeds the database on first boot, then serves the app.
set -e

if [ ! -f /data/recipes.db ]; then
  echo "==> No database at /data/recipes.db; seeding from recipes_json..."
  dotnet RecipeApp.dll seed
fi

exec dotnet RecipeApp.dll --urls http://0.0.0.0:5051
