# TICKET_8 Report — Web Recipe URL Importer (Deliverable 8) + Hands-Free Cook Mode & Print View (Deliverable 9)

**Date:** 2026-10-24 · **Status:** DONE — `dotnet build` green (only pre-existing NuGet advisory warnings), `svelte-check` 0 errors / 0 warnings (189 files), adapter-static SPA build green, and curl E2E against the live app on `:5051` passing (import, image serving, shopping-list store inheritance, and all failure paths).

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp/Services/RecipeUrlImporter.cs` | New — fetches a page with the shared `HttpClient`, locates the `schema.org/Recipe` JSON-LD node (top-level object, array item, or `@graph` entry; `@type` as string **or** array), and extracts title/description/servings/times/cuisine/categories/ingredients/steps/image. HTML entities decoded (`&amp;`, `&#39;`, …); ISO 8601 durations parsed (`PT20M`, `PT1H30M` → minutes); `recipeYield` first-integer parsing; `HowToStep[].text` and `HowToSection.itemListElement` trees flattened in order; `image` as string, string array, or `ImageObject`. Also `DownloadImageAsync` (streamed, 10 MB cap) |
| `api/RecipeApp/Controllers/RecipesController.cs` | + `POST /api/recipes/import-url` (`{ "url": "…" }`) — fetches, builds the `Recipe` through the normal repository path, best-effort downloads the hero photo (magic-byte validated via `ImageStore.DetectImageExtension`, ≤ 10 MB) and saves it as a cover image; returns `201 + RecipeDto`. Fetch/parse failures → `400` with a clear message; image failure never fails the import |
| `api/RecipeApp/Dtos/RecipeDtos.cs` | + `RecipeImportRequest(string Url)` |
| `api/RecipeApp/Program.cs` | `AddHttpClient<RecipeUrlImporter>` — browser-like `User-Agent`, 20 s timeout (pages **and** hero photos) |
| `client/src/lib/api.js` | + `importRecipeFromUrl(url)` |
| `client/src/lib/components/ImportUrlModal.svelte` | New — URL input, loading state (`"Importing recipe…"`), inline error display, Escape-to-close, auto-focus on open. Kept **always mounted** (hidden via CSS) so the `bind:this` input ref stays stable across open/close — a `#if`-wrapped `bind:this` trips svelte-check's `non_reactive_update` warning |
| `client/src/routes/+page.svelte` | + "Import from URL" button in the sidebar (below "+ Add New Recipe"); on success navigates straight to `/recipes/{newRecipe.id}` via `$app/navigation` |
| `client/src/lib/components/RecipeDetail.svelte` | **Cook Mode** toggle: Screen Wake Lock (`navigator.wakeLock?.request('screen')`) with graceful fallback when unsupported, re-acquire on `visibilitychange`, release on toggle-off / component destroy; "Screen Awake" badge while the lock is held; larger counter-distance text; tap-to-cross-off ingredients (local-only `Set` of indices) and tap-to-highlight the active step. + **Print** button (`window.print()`) |
| `client/src/app.css` | `.import-overlay`/`.import-modal` styles; `.cook-ingredients`/`.cook-steps` tappable rows (1.25 rem / 1.7 line-height, `.done` strikethrough, `.active` step highlight); `.wake-badge`; `@media print` hiding top nav, page nav, photo/scale/action rows, meal-plan form, modals, gallery chrome, and status spans |

## 2. Import pipeline semantics

- **Same normalization as seed.** Each JSON-LD ingredient line goes through `IngredientText.SplitLine`, then the repository's `FindOrCreateIngredientAsync` — which applies `IngredientCategoryClassifier.GuessCategory`, `IngredientDefaults` store hints (`kirkland`/`costco` → Costco), and pantry-staple detection. Imported ingredients therefore **dedupe against the shared catalog** and inherit preferred stores/pantry flags exactly like seeded ones (verified in §4).
- **Categories** merge `recipeCategory` + comma-split `keywords`, trimmed, capped at 8 × 40 chars.
- **Steps** cap at 200; sections are flattened with their names dropped (the app stores plain numbered steps).
- **Cover photo** is best-effort: if the download fails or the bytes aren't a recognized image format, the recipe still imports (201) without an image.

## 3. Cook mode semantics

- Wake lock is requested from the toggle click (user gesture, as the API requires). While cook mode is on, a `visibilitychange` listener re-acquires it when the tab becomes visible again (browsers drop locks for hidden tabs); the effect's cleanup releases it on toggle-off or component destroy. Unsupported browsers simply never show the "Screen Awake" badge — everything else works.
- Ingredient cross-offs and the active-step highlight are **local UI state only** (never persisted) — they reset when you leave the page, by design (prep progress is per-session).

## 4. E2E verification (live app on `:5051`)

Mock page served at `http://127.0.0.1:8765/recipe.html` with deliberately hard JSON-LD: `@graph` container, `"@type": ["Recipe"]` (array form), HTML entities in the title, ISO durations `PT20M`/`PT45M`, `recipeYield: "4 servings"`, a two-section `HowToSection` tree, and an `ImageObject` pointing at a local JPEG.

| Check | Result |
|---|---|
| `POST /api/recipes/import-url` | **201** with full `RecipeDto` |
| Title entities decoded | `"Torched Peach & Burrata Salad 'Test'"` ✓ |
| Times/servings | prep 20, cook 45, servings 4 ✓ |
| Cuisine / categories | `American`; `[Salad, Dinner, peach, burrata, summer, test]` (recipeCategory + keywords merged) ✓ |
| Ingredient splitting | `"2 lbs boneless chicken breasts"` → qty 2 lb; `"1 tsp salt (kirkland)"` → note extracted, **store detected as Costco** ✓ |
| Steps flattened | 4 steps from the two `HowToSection`s, in order ✓ |
| Cover photo downloaded + served | `GET /api/images/{id}/{file}` → **200**, `image/jpeg` ✓ |
| Shopping list built from imported recipe | `"boneless chicken breasts"` item arrived with `store: "Costco"` automatically (deduped against the existing catalog row) ✓ |
| Page without Recipe JSON-LD | **400** "No schema.org/Recipe JSON-LD found at that URL." ✓ |
| Unreachable host | **400** "Could not fetch that URL: Connection refused (127.0.0.1:9)" ✓ |
| Missing `url` field | **400** validation error ✓ |

Client build: `svelte-check` 0/0; the built bundle contains the import modal, `wakeLock`, cook-mode classes, and the print stylesheet. (Per work order, no headless Chromium was used — UI verified via svelte-check + bundle inspection + live API E2E.)

## 5. Cleanup

Test recipe, its shopping list, and its cover image were deleted through the API (`DELETE` → 204; image dir removed with the recipe). Two pure test ingredients (`burrata cheese`, `ripe peaches, halved`) were removed from the catalog; `fresh basil leaves` was **kept** — it's a shared catalog row also used by the existing "Basil Scramble" recipe. DB restored to its pre-test state (76 recipes, no orphan list items).
