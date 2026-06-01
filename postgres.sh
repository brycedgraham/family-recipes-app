#!/bin/bash
set -a
source .env
set +a

docker run -d \
  --name recipeapp-postgres \
  -e POSTGRES_USER=${DB_USER} \
  -e POSTGRES_PASSWORD=${DB_PASSWORD} \
  -e POSTGRES_DB=${DB_NAME} \
  -p 5432:5432 \
  -v recipeapp_postgres_data:/var/lib/postgresql/data \
  postgres:16