# TICKET_1 Report — SQLite Migration & Ingredient Normalization Upgrade

**Date:** 2026-10-16 · **Status:** DONE — build clean, re-seeded from scratch, all checks green.

## 1. What changed

| File | Change |
|---|---|
| `api/RecipeApp/appsettings.json` | Connection string → `Data Source=data/recipes.db` (repo-relative SQLite) |
| `api/RecipeApp/Program.cs` | `UseSqlite` + `SqliteDatabase.Resolve`; startup runs `EnsureCreatedAsync` so a fresh clone works before seeding |
| `api/RecipeApp.Infrastructure/RecipeApp.Infrastructure.csproj` | `Microsoft.EntityFrameworkCore.Sqlite 10.0.0` (Npgsql removed) |
| `api/RecipeApp.Infrastructure/Data/SqliteDatabase.cs` | New — rewrites relative `Data Source` against repo root, creates `data/`, forces `Foreign Keys=True` |
| `api/RecipeApp.Infrastructure/Migrations/20261016225055_InitialCreate.*` | Fresh SQLite migration (Npgsql one deleted per ticket) |
| `api/RecipeApp.Core/IngredientText.cs` | Normalization pipeline: unit canonicalization table, quantity parsing (fractions/decimals/ranges), size-qualifier notes, compound-quantity notes, `SplitNotes` (semicolon clauses, leading/trailing parentheticals, prep phrases, leading "of"), container-word stripping |
| `api/RecipeApp.Core/IngredientCategoryClassifier.cs` | New — ordered keyword rules → grocery-aisle category per ingredient |
| `api/RecipeApp.Core/Models/Ingredient.cs` | Added `Category` property |
| `extract_recipes.py` | All-caps subheadings (`FOR THE FILLING`) now route to instructions instead of leaking into ingredients (5 lines across 3 recipes) |
| `AGENTS.md`, `ROADMAP.md`, `.gitignore` | Updated for the new stack; `data/` gitignored except `.gitkeep` |
| `postgres.sh` | Deleted (obsolete after cutover) |
| `data/recipes.db` | Seeded: 76 recipes, 365 ingredients, 620 recipe-ingredient rows |

## 2. How normalization works now

Each raw ingredient line from `recipes_json/*.json` passes through `IngredientText.SplitLine`:

1. **Clean:** unicode fractions → ASCII (`½` → `1/2`), markdown emphasis/bullets stripped, label prefixes (`optional: …`) removed.
2. **Quantity:** leading fraction (`1/2`, `3 1/4`), decimal (`0.5`), or plain number; then range tails consumed — `5-6 eggs`, `1 1/2 - 2 cups`, `6 to 8 ounces`, `1 or 2 bay leaves` (first value kept, by design for shopping lists).
3. **Size qualifier:** `9 oz. bag …` / `32 oz box …` inside the name moves to Notes as `(9 oz. bag)`; parsing continues on the remainder.
4. **Unit:** first word matched against an alias table (case-insensitive): `t/T/tsp/teaspoons → tsp`, `tbsp/Tb./tablespoons → tbsp`, `c/cup(s) → cup`, `oz/ounces → oz`, `lb/pounds → lb`, `can(s) → can`, `pkg/package/packet → pkg`, plus `clove, slice, head, dash, bag, box, bottle, jar, bunch, stick, sprig, stalk, pinch, pt, qt, gal, ml, l, g, kg`. Unknown words (`eggs`, `fl`) are **not** units — they stay in the name.
5. **Compound quantity:** `1 cup + 2 tbsp X` → keep first quantity, note `plus 2 tbsp`.
6. **Notes** (`SplitNotes`, in order): semicolon clauses; leading parenthetical after the unit — `(8 ounces) bow tie pasta` → note `(8 ounces)` (with gram-marker fold: `(70)g melted butter` → note `(70) g`, name `melted butter`); stacked trailing parentheticals — `diced tomatoes (undrained) (Blender these)` → both to notes; comma prep phrases from a whitelist (`diced, chopped, roughly chopped, bite size pieces, casings removed, grated and squeezed dry, …`).
7. **Name cleanup:** leading container word dropped when it follows a measurement (`15 oz can diced tomatoes` → `diced tomatoes`); leading `of` dropped after a container unit (`1 packet of biscuits` → `biscuits`).
8. **Normalize:** `NormalizedName = name.ToLowerInvariant()` — unique index, drives cross-recipe dedup. Display `Name` keeps source casing.
9. **Category:** `IngredientCategoryClassifier.GuessCategory(normalizedName)` — 11 grocery aisles, ordered keyword rules (specific compounds before generic words; e.g. `tomato sauce` → Canned & Jarred beats `tomato` → Produce). No match → `Other`.

## 3. Before / after samples

| Raw line | Before (qty/unit/name) | After (qty/unit/name — notes · category) |
|---|---|---|
| `1/2 pkg (8 ounces) bow tie pasta` | 0.5 / pkg / `(8 ounces) bow tie pasta` | 0.5 / pkg / `bow tie pasta` — "(8 ounces)" · Dry Goods & Pasta |
| `2 pounds boneless chicken breasts; cut into bite-size pieces` | 2.0 / pounds / `boneless chicken breasts; cut into bite-size pieces` | 2.0 / lb / `boneless chicken breasts` — "cut into bite-size pieces" · Meat & Seafood |
| `5-6 eggs` | 5.0 / — / `-6 eggs` | 5.0 / — / `eggs` · Dairy & Eggs |
| `1 (14.5 oz) can diced tomatoes (undrained) **(Blender these. Cooked tomato chunks are gross)**` | 1.0 / — / `(14.5 oz) can diced tomatoes (undrained) **(blender these…)**` | 1.0 / — / `diced tomatoes` — "(14.5 oz); (Blender these. Cooked tomato chunks are gross); (undrained)" · Produce |
| `½ cup Butter (or 8 Tbsp.)` | — / — / `½ cup butter (or 8 tbsp.)` (quantity unparsed) | 0.5 / cup / `Butter` — "(or 8 Tbsp.)" · Dairy & Eggs |
| `1/2 9 oz. bag fresh spinach` | 0.5 / — / `9 oz. bag fresh spinach` | 0.5 / — / `fresh spinach` — "(9 oz. bag)" · Produce |
| `3 / 4  cup chopped yellow onion (1/2 medium)` | 3.0 / — / `/ 4  cup chopped yellow onion (1/2 medium)` | 0.75 / cup / `chopped yellow onion` — "(1/2 medium)" · Produce |
| `4 Tb. green onions` | 4.0 / — / `Tb. green onions` | 4.0 / tbsp / `green onions` · Produce |
| `1 or 2 bay leaves` | 1.0 / — / `or 2 bay leaves` | 1.0 / — / `bay leaves` · Spices & Seasonings |

## 4. Database statistics (after re-seed)

**Row counts**

| Table | Rows |
|---|---|
| Recipes | 76 |
| Ingredients | 365 |
| RecipeIngredients | 620 |
| RecipeSteps | 209 |
| Categories (recipe M2M) | 9 |

**Dedup effect:** 464 unique ingredient names under the old logic (quantity + first word as unit, nothing else) → **365** in the new DB (−99, −21%). All 620 ingredient lines map to a clean name; zero rows with leading/trailing punctuation or markdown residue.

**Unit distribution** (`RecipeIngredients.Unit`)

| Unit | Rows | | Unit | Rows |
|---|---|---|---|---|
| (null) | 212 | | can | 12 |
| cup | 153 | | clove | 10 |
| tsp | 107 | | pkg | 4 |
| tbsp | 76 | | slice / head / dash / bag | 1 each |
| oz | 21 | | | |
| lb | 21 | | | |

**Ingredient category distribution** (`Ingredients.Category`)

| Category | Ingredients | | Category | Ingredients |
|---|---|---|---|---|
| Produce | 86 | | Dry Goods & Pasta | 19 |
| Spices & Seasonings | 62 | | Baking | 11 |
| Meat & Seafood | 49 | | Bakery & Bread | 11 |
| Dairy & Eggs | 49 | | Frozen | 9 |
| Oils & Condiments | 40 | | Other | 8 |
| Canned & Jarred | 21 | | | |

**Notes:** 115 / 620 rows (18.5%) carry a `RecipeIngredient.Notes` value.

**Sample ingredients (name · category)**

| Name | Category |
|---|---|
| boneless chicken breasts | Meat & Seafood |
| bow tie pasta | Dry Goods & Pasta |
| chicken broth | Canned & Jarred |
| diced tomatoes | Produce |
| eggs | Dairy & Eggs |
| fresh spinach | Produce |
| ginger | Produce |
| melted butter | Dairy & Eggs |
| olive oil | Oils & Condiments |
| part skim ricotta cheese | Dairy & Eggs |

**Sample recipe-ingredient rows with notes (qty / unit / name — notes)**

| Row |
|---|
| — / — / Bok Choy — "cut to bite size pieces" |
| 3.0 / cup / Broccoli — "bite size pieces" |
| 0.5 / cup / Butter — "(or 8 Tbsp.)" |
| 0.25 / cup / Butter — "softened" |
| 6.0 / oz / Cellentani pasta — "(corkscrew)" |
| — / — / Cilantro — "minced" |
| 4.0 / clove / Garlic — "minced" |
| 3.0 / clove / Garlic — "minced" |
| 15.0 / oz / diced tomatoes — (none) |
| 1.0 / — / diced tomatoes — "(14.5 oz); (Blender these. Cooked tomato chunks are gross); (undrained)" |

## 5. Verification performed

- `dotnet build api/RecipeApp.sln` — 0 errors.
- `rm data/recipes.db && dotnet run --project api/RecipeApp -- seed` → `76 added, 0 skipped, 0 failed`.
- Seed idempotency: second run (no delete) → `0 added, 76 skipped, 0 failed` in 2.3 s.
- Cold start: with no DB file, API boots and auto-creates the schema — `GET /api/recipes` → 200 (empty), `GET /api/categories` → 200.
- Extractor diff old-vs-new classification: exactly 5 all-caps subheading lines moved ingredients→instructions (3 recipes); nothing else changed; regenerated JSON matches a fresh parse of `Recipes.md` with 0 mismatches.
- DB audits: zero malformed names (no leading/trailing punctuation, no markdown residue); targeted checks for every fix class (dedup of `melted butter`, `green onions` via `Tb.`, `biscuits` from `packet of`, `bay leaves` from `1 or 2`).

## 6. Known limitations

- **SQLite `LIKE` is case-insensitive only for ASCII** — the `?q=` search will not case-fold non-ASCII ingredient names.
- **EF Core stores `decimal?` as TEXT in SQLite.** Quantities are rounded to 4 places at seed time and treated as display values by the API/client (no arithmetic) — acceptable for this app; revisit if quantity math is ever needed.
- **Ranges keep the first value** (`5-6 eggs` → 5, `1 or 2 bay leaves` → 1) — deliberate for shopping lists; the second value is not preserved anywhere.
- **Comma-less prep adjectives stay in the name** (`chopped yellow onion`, not `yellow onion`). Only comma-separated trailing phrases move to Notes — moving leading adjectives would break names where they're essential (`part skim ricotta cheese`, `crushed red pepper flakes`).
- **Mid-name parentheticals stay in the name** (5 cases: `Penne (tube) noodles`, `At least 4 small (winco) Potatoes`, …) — they disambiguate rather than annotate.
- **No synonym merging beyond exact normalized names:** `onion` vs `yellow onion` vs `green onions` remain distinct ingredients. A thesaurus pass would be a follow-up if shopping lists need it.
- **Classifier is keyword-based:** 8/365 names land in `Other` (water, warm water, Coke NOT diet, Salad, Toppings, Cooking spray, Slow Cooker, chokecherries [sic]) — all genuinely unclassifiable or typos in the source.
