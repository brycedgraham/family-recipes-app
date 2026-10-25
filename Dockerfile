# syntax=docker/dockerfile:1
# Family Recipes — single-container production image.
# Serves the SPA, REST API, SQLite database, and recipe images on one port (5051),
# mirroring the native ./start.sh single-process deployment.

# --- Stage 1: build the SvelteKit SPA ---------------------------------------
FROM node:22-alpine AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# --- Stage 2: publish the .NET API ------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY api/RecipeApp.sln ./
COPY api/RecipeApp/ RecipeApp/
COPY api/RecipeApp.Core/ RecipeApp.Core/
COPY api/RecipeApp.Infrastructure/ RecipeApp.Infrastructure/
RUN dotnet publish RecipeApp/RecipeApp.csproj -c Release -o /out

# --- Stage 3: runtime ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV ConnectionStrings__DefaultConnection="Data Source=/data/recipes.db"
WORKDIR /app
COPY --from=api /out ./
# Program.cs derives the SPA directory as <parent of data dir>/client/dist, so with the
# database at /data/recipes.db the built SPA must live at /client/dist inside the image.
COPY --from=client /src/client/dist /client/dist
# First-boot seeding source (see docker-entrypoint.sh).
COPY recipes_json/ /app/recipes_json/
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh
EXPOSE 5051
ENTRYPOINT ["docker-entrypoint.sh"]
