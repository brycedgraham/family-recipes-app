# TICKET_11 Report — Android PWA Installability + Real Ponderosa Bark Texture

## Step 1: `bark.webp` from the user's photo

- 5-line Pillow script (Pillow 13.0.0): opened `client/static/textures/raw_bark.jpg`, applied
  `ImageOps.exif_transpose`, `thumbnail((1024, 1024), LANCZOS)`, saved as WEBP quality 82.
- Result: `client/static/textures/bark.webp` — **513×360 RGB, 62 KB**. The source photo was
  already under the 1024 px cap, so no downscaling occurred; the file is at native resolution.
- `raw_bark.jpg` left untouched. Visually verified: true Ponderosa bark plates and vertical crevices, warm rust/amber tones.

## Step 2: CSS wiring (`client/src/app.css`)

- Removed the striped `repeating-linear-gradient` from `--bark-texture`. Replaced with two variables:
  - `--bark-photo: url('/textures/bark.webp')`
  - `--bark-overlay: linear-gradient(rgba(32, 20, 14, 0.50), rgba(24, 15, 10, 0.65))` (light)
- `[data-theme="dark"]` overrides `--bark-overlay` with
  `linear-gradient(rgba(18, 12, 9, 0.62), rgba(14, 9, 7, 0.76))`.
- `.App-sidebar` and `.App-topnav` now use
  `background-image: var(--bark-overlay), var(--bark-photo); background-size: cover; background-position: center;`
  (the previous vertical gradient layer was dropped in favor of the photo + umber overlay).
- Added `text-shadow: 0 1px 2px rgba(0, 0, 0, 0.7)` to `.CategoryList .category-item` and `.App-topnav a`.

## Step 3: PWA installability

| File | Change |
|---|---|
| `client/src/app.html` | Manifest link now `<link rel="manifest" href="/manifest.webmanifest" crossorigin="use-credentials" />` |
| `client/static/manifest.webmanifest` | Added `"id": "/"`; kept `start_url`/`scope` `/`, `display: standalone`; `theme_color` → `#2d5a3f`; `background_color` → `#f8f5ee`; icon entries for 192×192 and 512×512 PNGs (`purpose: any`). Icons verified with Pillow: both genuine RGBA PNGs at exact sizes. |
| `client/src/service-worker.js` (new) | Minimal SW: `install` → `skipWaiting()`, `activate` → `clients.claim()`, `fetch` → network pass-through. SvelteKit compiles it to `/service-worker.js` and auto-registers in production builds (`kit.serviceWorker.register` defaults to `true`). |
| `api/RecipeApp/Program.cs` | SPA `UseStaticFiles` now gets a `FileExtensionContentTypeProvider` mapping `.webmanifest` → `application/manifest+json`. Note: .NET 10's default provider already served this MIME (verified against the pre-change image), so this is an explicit, defensive registration. |
| `client/jsconfig.json` | Exclude entry changed `"src/service-worker"` → `"src/service-worker.js"`. Kit's tsconfig validation warned because TS does not match the extensionless pattern against `.js` files, leaving the new SW file in the program; the explicit `.js` pattern clears it (verified: 0 errors / 0 warnings). |

## Step 4: Build & verify

- `dotnet build api/RecipeApp.sln` — **0 errors** (10 pre-existing NU1902/NU1903 package-vulnerability
  advisories, none from this change).
- `npm --prefix client run check` — **0 errors, 0 warnings** (189 files).
- `npm --prefix client run build` — success; `service-worker.js` emitted to `dist/`.
- Production restart: on this host the production server is the Docker container
  (`docker compose`, `restart: unless-stopped`, publishes `5051`), so the rollout was
  `docker compose up -d --build` rather than bare `./start.sh` (which cannot bind 5051 while the
  container owns it). Container recreated from a fresh image in ~19 s.

`curl -I http://localhost:5051/...` against the new image:

| Endpoint | Status | Content-Type | Size |
|---|---|---|---|
| `/textures/bark.webp` | 200 OK | `image/webp` | 63,668 B |
| `/manifest.webmanifest` | 200 OK | `application/manifest+json` | 539 B (new manifest) |
| `/service-worker.js` | 200 OK | `text/javascript` | 319 B |

Also verified: served `/` contains the `crossorigin="use-credentials"` manifest link, and the served
`/service-worker.js` is the compiled install/activate/fetch worker.

## Notes

- The bark source photo is only 513×360; with `background-size: cover` on a full-height sidebar it is
  upscaled ~2–3×, so the texture reads as soft photographic grain rather than crisp detail. A higher-resolution
  photo would improve this without any code change.
- `raw_bark.jpg` (87 KB) is still copied to `dist/` by SvelteKit's static pipeline; it is not referenced by
  the app. Move it out of `client/static/` if shipping it to clients should be avoided.
