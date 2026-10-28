# TICKET_5 Report — Recipe Images + Single-Process Production Hosting + Installable PWA

**Date:** 2026-10-23 · **Status:** DONE — `dotnet build` 0 errors, `svelte-check` 0 errors / 0 warnings (187 files), adapter-static SPA build green, curl + headless-Chromium E2E against the single process on `:5051` **37/37 assertions** passing.

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp.Core/Models/RecipeImage.cs` | New — `Id`, `RecipeId`, `FileName`, `SortOrder`, `CreatedAt`; no constructor GUID (EF generates keys) |
| `api/RecipeApp.Core/Models/Recipe.cs` | + `List<RecipeImage> Images` navigation property |
| `api/RecipeApp.Infrastructure/Data/RecipeDbContext.cs` | `DbSet<RecipeImage>`; cascade delete `Recipe → Images`; images ordered by `SortOrder`, then `CreatedAt` |
| `api/RecipeApp.Infrastructure/Migrations/*_AddRecipeImages.cs` | New migration — `RecipeImages` table, FK → `Recipes` cascade |
| `api/RecipeApp.Infrastructure/Repositories/RecipeRepository.cs` | `.Include(r => r.Images)` on all recipe queries; delete path removes the recipe's image directory from disk |
| `api/RecipeApp/Services/ImageStore.cs` | New — owns photo files under `data/images/{recipeId}/{guid}{ext}`: extension allow-list (`.jpg/.jpeg/.png/.webp/.gif`) + 10 MB cap, save / delete file (+ removes the per-recipe dir when it empties) / delete whole recipe dir |
| `api/RecipeApp/Dtos/RecipeDtos.cs` | + `ImageUrls` (`/api/images/{recipeId}/{fileName}`, ordered) on `RecipeDto` |
| `api/RecipeApp/Controllers/RecipesController.cs` | + `POST /api/recipes/{id}/images` (multipart `IFormFileCollection files`, 400 on bad extension/size, returns updated `RecipeDto`) · + `DELETE /api/recipes/{id}/images/{fileName}` (row + file removed); recipe DELETE also clears `data/images/{id}` |
| `api/RecipeApp/Controllers/MealPlansController.cs` | Step 1 — when an entry is added/patched with a `RecipeId` and no explicit `CustomTitle`, the resolved `Recipe.Title` is snapshotted into `CustomTitle`, so the entry keeps its name after the recipe is deleted (`ON DELETE SET NULL`) |
| `api/RecipeApp/Program.cs` | Single-process hosting: when `client/dist` exists → `UseDefaultFiles` + `UseStaticFiles` (SPA) and `MapFallbackToFile("index.html")`; images served at `/api/images` via `PhysicalFileProvider`; catch-all `Map("/api/{**rest}")` keeps unknown API routes 404ing as JSON; **explicit `app.UseRouting()` between static files and `MapControllers`** (see §5) |
| `client/src/lib/api.js` | + `uploadRecipeImage(recipeId, file)` (multipart), `deleteRecipeImage(recipeId, fileName)` |
| `client/src/lib/components/RecipeDetail.svelte` | Hero + thumbnail gallery when photos exist (thumbnails only for >1 photo; clicking a thumbnail switches the hero); **Upload Photo** button (`<input type="file" accept="image/*">`, mobile-friendly) and per-photo × remove on hero and each thumbnail; local state override so the UI updates without a refetch |
| `client/src/lib/components/RecipeForm.svelte` | Multi-file photo picker on create **and** edit (uploaded via `uploadRecipeImage` right after POST/PUT succeeds); existing photos removable in edit mode |
| `client/src/lib/components/RecipeCard.svelte` | First image renders with `object-fit: cover`; clean `.no-image` placeholder when unphotographed |
| `client/static/manifest.webmanifest` | New — name "Family Recipes", short_name "Recipes", `display: standalone`, `start_url: "/"`, theme `#e76f51`, background, SVG + 192/512 PNG icons |
| `client/static/favicon.svg` · `icon-192.png` · `icon-512.png` | New — culinary bowl icon (hand-authored SVG; PNGs rasterized from it) |
| `client/src/app.html` | Manifest link, `theme-color`, `mobile-web-app-capable`, `apple-mobile-web-app-capable`, `apple-touch-icon`, and document `<title>` |
| `start.sh` | New — one-command production launcher: builds `client/dist` + Release API, then runs the single .NET process on `http://0.0.0.0:5051` from the app's base directory (so `appsettings.json` and repo-root data resolution work) |
| `AGENTS.md` · `ROADMAP.md` | Updated: image storage strategy, production hosting/PWA notes, `start.sh` command, middleware-ordering gotcha |

## 2. Image API surface

| Endpoint | Behavior |
|---|---|
| `POST /api/recipes/{id}/images` | Multipart field `files` (one or more). Rejects disallowed extensions and >10 MB with 400. Saves as `data/images/{id}/{guid}{ext}`, inserts `RecipeImage` rows, returns the updated `RecipeDto` with fresh `imageUrls` |
| `DELETE /api/recipes/{id}/images/{fileName}` | Deletes the row + the physical file; removes the now-empty per-recipe directory. Returns the updated `RecipeDto` |
| `GET /api/images/{id}/{fileName}` | Static file served by `PhysicalFileProvider` (same origin as the API → works through the Vite dev proxy and single-process prod identically) |
| `DELETE /api/recipes/{id}` | Existing endpoint now also removes `data/images/{id}/` recursively |

## 3. Single-process production hosting

`./start.sh` builds everything and runs **one** process on `http://0.0.0.0:5051`: UI + API + SQLite + images. Pipeline order in `Program.cs`:

1. `UseDefaultFiles` / `UseStaticFiles` → built SPA from `client/dist` (only when the directory exists — dev mode is unaffected)
2. `UseStaticFiles` → photos at `/api/images`
3. **`app.UseRouting()`** (explicit — see §5)
4. `MapControllers()`, then `MapFallbackToFile("index.html")` for unmatched non-API routes, and a `Map("/api/{**rest}")` catch-all so unknown API paths return 404 JSON, never the SPA shell

## 4. Verification (Step 5)

- `dotnet build api/RecipeApp.sln` — 0 errors (remaining output is pre-existing NuGet audit advisories NU1902/NU1903 on transitive packages — out of scope).
- `npm --prefix client run check` — **187 files, 0 errors, 0 warnings**.
- `npm --prefix client run build` — static SPA written to `client/dist`.
- E2E: curl + Playwright headless Chromium (cached `chromium-1248`, libs staged in `/tmp/chromedeps`) against the **single-process server on :5051** — **37/37 PASS**:
  - `/` serves the SPA from :5051 (title "Family Recipes", 76 recipe cards); `/manifest.webmanifest` → 200 with correct name/display/start_url/icons; `GET /api/does-not-exist` → 404 JSON (not `index.html`); client route `/meal-plan` renders via the SPA fallback.
  - Uploaded a real PNG + a real WebP (WebP captured from Chromium via CDP `Page.captureScreenshot`) to the Alfredo recipe: both accepted, `imageUrls` grew by 2, each URL returned **200 with valid magic bytes** (`\x89PNG…`, `RIFF…webp`), and both images **decoded in Chromium** (`naturalWidth=128`) on the home `RecipeCard` and the detail hero. A `.txt` upload was rejected with 400.
  - Gallery: two thumbnails rendered, clicking the second switched the hero; removing a photo via the UI's × dropped the DB row **and** the file on disk, and the thumbnail strip correctly hides when one photo remains. Unphotographed recipes show the placeholder, no broken `<img>`.
  - MealPlan fallback: created a temp recipe, scheduled it (no custom title) → title snapshotted into `CustomTitle`; deleted the recipe (204) → its image directory was removed from disk and the meal-plan entry **still displays the recipe's title** with `recipeId: null`.
  - Cleanup verified: test recipe/entry removed, Alfredo back to zero images, no orphan files or empty directories under `data/images/`.

## 5. Bug found & fixed during E2E (the interesting one)

Every `GET /api/images/{id}/{fileName}` returned **404 JSON** even though the file existed on disk and the upload endpoint worked. Reproduced in a minimal .NET 10 app: with no explicit `UseRouting`, **endpoint matching runs ahead of `UseStaticFiles`**, so the `Map("/api/{**rest}")` catch-all swallowed every image request (the SPA static files masked the problem because unmatched non-API paths were served by the fallback endpoint anyway). Fix: an explicit `app.UseRouting()` between the static-file middleware and `MapControllers` — verified 200 in the minimal repro, then end-to-end. Documented as a gotcha in `AGENTS.md` and the ROADMAP decisions log.

Two smaller fixes from the same pass: `app.html` had no `<title>` (browser tab + PWA name), and deleting a recipe's last photo left an empty `data/images/{id}/` directory behind — `ImageStore.DeleteFile` now removes the directory when it empties.

## 6. How to run

```bash
./start.sh        # production: one process on http://0.0.0.0:5051 (UI + API + images)
# dev, as before:
dotnet run --project api/RecipeApp   # API on :5051
npm --prefix client run dev          # Vite with /api proxy
```

On a phone/LAN device: open `http://<box-ip>:5051` → "Add to Home Screen" installs the PWA (standalone, bowl icon, terracotta theme).
