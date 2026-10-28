# TICKET_15 Report — Opportunistic Async Photo Recipe Importer

Local OpenAI-compatible vision endpoint (`:8888`) now ingests cookbook photos asynchronously: upload → staged queue → background vision extraction → normalized catalog recipe with the original photos promoted to the gallery. When the GPU is offline, jobs simply queue and import automatically once it returns.

## What Was Built

### Step 1 — Configuration
- `.env` already defined `VISION_ENDPOINT_URL` pointing at the LAN GPU host (`http://<gpu-host>:8888`), so no LAN subnet probe was needed (per work order: probe only when unset).
- `docker-compose.yml` now passes into the container:
  - `VisionImport__EndpointUrl: ${VISION_ENDPOINT_URL:-…}` — interpolates `.env`, falling back to a loopback address (endpoint simply reports offline and jobs queue)
  - `VisionImport__PollIntervalSeconds: 30`

### Step 2 — `RecipeImportJob` entity & migration (`api/`)
- `RecipeApp.Core/Models/RecipeImportJob.cs`: `Id` (no constructor GUID, per repo rule), `Status` (required, max 20), `FileNamesJson`, `Category?` (100), `RecipeId?`, `RecipeTitle?` (200), `ModelUsed?` (150), `Error?` (1000), `CreatedAt`, `CompletedAt?`.
- `RecipeDbContext.RecipeImportJobs` DbSet added (all existing DbSets preserved); migration `AddRecipeImportJobs` created and applied to `data/recipes.db`.
- On startup the worker resets any jobs stuck in `"Processing"` (interrupted container restart) back to `"Pending"` — verified in container logs (`UPDATE "RecipeImportJobs" SET "Status" = ... WHERE "Status" = 'Processing'`).

### Step 3 — Opportunistic background worker (`api/RecipeApp/Services/`)
- `VisionImportWorker.cs` (`BackgroundService`, registered in `Program.cs`):
  - **Probe:** `GET {EndpointUrl}/v1/models` (3 s timeout); on 200 parses `data[0].id` to auto-discover the loaded model. Verified live: discovered `unsloth/Qwen3.8-Flash-Next-GGUF`.
  - **Loop:** every `PollIntervalSeconds` (default 30) or immediately on wake signal after upload. Pending jobs + offline endpoint → one Debug-level log, jobs stay `Pending` (never failed).
  - **Extraction:** job → `"Processing"`; staged images base64 data-URLs; `POST /v1/chat/completions` (180 s timeout, `temperature 0.1`) with a strict JSON-only prompt (`title/description/category/servings/prepTimeMinutes/cookTimeMinutes/ingredients[]/steps[]`); markdown code fences stripped before `JsonSerializer` parse.
  - **Ingestion:** reuses the URL-importer pipeline (`IngredientText.SplitLine`, `RecipeRepository.FindOrCreateIngredientAsync`, `IngredientCategoryClassifier`, `IngredientDefaults`) so ingredients normalize and inherit WinCo/Costco routing + pantry defaults.
  - **Promotion:** staged photos move to `{dataDir}/images/{recipe.Id}/` with `RecipeImage` rows; queue dir removed; job → `Completed` (+`RecipeId`, `RecipeTitle`, `ModelUsed`, `CompletedAt`). On exception: `Failed` + concise `Error`, staged files kept for Retry.
- Support services: `VisionEndpointStatus.cs` (singleton live status), `VisionImportWakeSignal.cs` (`Channel` wake), `ImportQueueStore.cs` (staging dir helpers).

### Step 4 — API endpoints (`api/RecipeApp/Controllers/PhotoImportsController.cs`)
- `POST /api/recipes/import-photo` — multipart `files` (1–4, `.jpg/.jpeg/.png/.webp`) + optional `category`; stages to `{dataDir}/import_queue/{jobId}/`, inserts `Pending` job, wakes worker, returns `202` + DTO.
- `GET /api/recipes/import-photo/jobs` — `{ endpointOnline, activeModel, endpointUrl, jobs }` (newest 25).
- `POST /api/recipes/import-photo/jobs/{id}/retry` — `Failed` → `Pending`, clears `Error`, wakes worker.
- `DELETE /api/recipes/import-photo/jobs/{id}` — removes row + staged dir.

### Step 5 — Svelte 5 UI (`client/`)
- `src/lib/api.js`: `uploadRecipePhotos` (multipart, no manual `Content-Type`), `getPhotoImportJobs`, `retryPhotoImportJob`, `deletePhotoImportJob`.
- `src/lib/components/PhotoImportModal.svelte` (runes only — `$state`/`$derived`/`$props`): live status pill (green `GPU Online (model)` / amber `GPU Offline — Photos will queue & import automatically`), file picker + camera capture input (1–4 photos), **canvas resize to 1600 px longest edge @ JPEG 0.85 before upload**, recent-jobs list with status badges, `Completed` → link to `/recipes/{id}`, `Failed` → error + Retry/Delete; 4 s polling while any job is active; completion triggers recipe-list refresh in the parent.
- `src/routes/+page.svelte`: **"Import from Photo"** secondary button below "Import from URL" with live `(n)` badge for Pending+Processing jobs; modal wired with `onRecipesChanged` (bumps a `refreshToken` read inside the recipe-list `$effect`) and `onActiveChange`.

## Verification (Step 6)
- `dotnet build api/RecipeApp.sln`: 0 errors (10 pre-existing NU1903/transitive warnings, unchanged).
- `npm --prefix client run check`: **190 files, 0 errors, 0 warnings**; `npm --prefix client run build` clean.
- `docker compose up -d --build`: container recreated; startup log shows stuck-job reset SQL and probe success: `Vision endpoint online at http://<gpu-host>:8888 (model: unsloth/Qwen3.8-Flash-Next-GGUF)`.
- `GET /api/recipes/import-photo/jobs` → `200`, `endpointOnline: true`, live model + URL, `jobs: []`.
- `POST /api/recipes/import-photo` (synthetic noise PNG) → `202`, job `Pending`, staged file present at `data/import_queue/{jobId}/1-….png`.
- Worker picked it up immediately (wake signal): `Processing` → real inference round-trip (14.3 s, HTTP 200) → correctly rejected the noise image with `Failed: vision model did not extract a recipe title` — extraction guard works; no junk recipe created (recipe count steady at 77).
- `DELETE /api/recipes/import-photo/jobs/{id}` → `204`; queue dir removed, `jobs: []` afterwards.

## Notes / Gotchas
- Staged upload filenames are `{index}-{guid}{ext}` to keep vision-message ordering stable and avoid client filename collisions.
- The endpoint being online at deploy time means photo imports process within seconds; offline periods cost nothing — jobs queue and the loop retries every 30 s.
- A `Failed` job keeps its staged photos until Retry or Delete, per spec.

## Follow-up (2026-10-28): live status pill + auth-gated endpoints
- The status pill froze at the last probe because the worker only probed when jobs were queued (opportunistic design). Now `ProcessPendingJobsAsync` probes **every pass** (~30 s even with an empty queue), so model swaps, outages, and auth changes are reflected within one interval. Verified live: after the GPU host started requiring auth, the pill flipped to Offline within seconds instead of showing a stale model.
- Auth-gated vision endpoints (401 on `/v1/models`): set `VISION_API_KEY` in `.env` → passed as `VisionImport__ApiKey` → Bearer header on probe + inference. Empty/unset = no header (unchanged behavior). Note: this repo's `.env` has CRLF line endings; the worker trims the key defensively.
