# Recipe Management App — Project Plan

A family recipe management web application built with ASP.NET Core 8, React + Vite, and SQL Server.

---

## Table of Contents

- [Tech Stack](#tech-stack)
- [Architecture Overview](#architecture-overview)
- [Phase 1 · Foundation](#phase-1--foundation)
- [Phase 2 · Core Recipe App](#phase-2--core-recipe-app)
- [Phase 3 · Users & Authentication](#phase-3--users--authentication)
- [Phase 4 · Shopping Lists](#phase-4--shopping-lists)
- [Phase 5 · Meal Planning](#phase-5--meal-planning)
- [Phase 6 · Polish & Scale](#phase-6--polish--scale)
- [Key Milestones](#key-milestones)
- [Database Schema (Overview)](#database-schema-overview)

---

## Tech Stack

| Layer | Choice | Notes |
|---|---|---|
| Backend | ASP.NET Core 8 Web API | Minimal APIs or Controllers |
| Frontend | React + Vite | React Query (TanStack) for data fetching |
| Database | SQL Server or PostgreSQL | EF Core code-first with migrations |
| ORM | Entity Framework Core | Repository pattern |
| Auth | ASP.NET Core Identity + JWT | Bearer tokens + refresh tokens |
| Validation | FluentValidation (backend), Zod (frontend) | |
| Forms | React Hook Form | |
| Drag & Drop | @dnd-kit/core | For meal planner |
| Containerization | Docker + docker-compose | API + DB in dev |
| CI/CD | GitHub Actions | Build, test, lint, deploy |
| Hosting | Azure / Railway / Fly.io | Dockerized |

---

## Architecture Overview

```
solution/
├── RecipeApp.Api/            # ASP.NET Core Web API (routing, controllers, middleware)
├── RecipeApp.Core/           # Domain models, interfaces, service contracts
├── RecipeApp.Infrastructure/ # EF Core DbContext, repositories, migrations
├── RecipeApp.Tests/          # xUnit unit + integration tests (in-memory EF)
└── client/                   # Vite + React frontend
    ├── src/
    │   ├── components/
    │   ├── pages/
    │   ├── hooks/            # React Query hooks
    │   ├── api/              # Axios client + API functions
    │   └── lib/              # Utilities, Zod schemas
    └── ...
```

**Key design decisions:**
- `Core` project has zero infrastructure dependencies — domain models and service interfaces only.
- `Infrastructure` implements EF Core repositories, registered via DI in `Api`.
- Frontend state lives in React Query — no global state manager needed initially.
- JWT access tokens stored in memory; refresh tokens in HttpOnly cookies.

---

## Phase 1 · Foundation

**Estimated time:** 1–2 weeks  
**Goal:** Project structure, tooling, CI/CD, and database skeleton before any feature code.

### Project Setup

- [ ] Create solution with `RecipeApp.Api`, `RecipeApp.Core`, `RecipeApp.Infrastructure`, and `RecipeApp.Tests` projects
- [ ] Initialize Git repo with `main` + `dev` branches; protect `main` with PR reviews; use conventional commits
- [ ] Add `docker-compose.yml` with API container and SQL Server (or Postgres) container for dev parity
- [ ] Configure GitHub Actions: build, test, and lint on every PR; deploy to staging on merge to `dev`

### Database Schema (Initial)

- [ ] Design initial EF Core schema:
  - `Recipes` (id, title, description, servings, prepTimeMinutes, cookTimeMinutes, instructions, cuisine, createdAt, updatedAt)
  - `Ingredients` (id, name, normalizedName)
  - `RecipeIngredients` (id, recipeId, ingredientId, quantity, unit, notes) — join table
  - `Tags` (id, name)
  - `RecipeTags` (recipeId, tagId) — join table
  - Plan for `Users`, `ShoppingLists`, and `MealPlans` in later phases
- [ ] Run first EF Core migration (`dotnet ef migrations add InitialCreate`) and confirm migrations run automatically in dev startup

### API & Frontend Skeleton

- [ ] Scaffold ASP.NET Core Web API: Swagger/OpenAPI, global exception handling middleware, problem details responses, CORS policy for the React dev server
- [ ] Scaffold React + Vite frontend: TanStack Query, React Router v6, component library (shadcn/ui or Radix UI), environment variable for API base URL
- [ ] Add xUnit test project with an in-memory EF Core provider for integration tests; add Vitest for frontend

---

## Phase 2 · Core Recipe App

**Estimated time:** 2–3 weeks  
**Goal:** Parse the existing markdown file, import all recipes into the database, build the CRUD API, and display them in a React UI with search and filtering.

### Markdown Import

- [ ] Audit the markdown file format — define a parsing spec covering: heading structure, ingredient list format (quantity, unit, name), metadata fields (servings, cook time, cuisine, tags), and instructions format
- [ ] Build a markdown-to-domain parser as a C# service using `Markdig`; parse into `Recipe` domain objects; handle malformed entries gracefully with logged warnings
- [ ] Build a one-time seed/import command (`dotnet run --import recipes.md`) or a secured `POST /admin/import` endpoint that parses and upserts recipes into the DB
- [ ] Write unit tests for the parser: happy paths, missing fields, unusual ingredient formats, Unicode, recipes without tags

### Recipe API

- [ ] Implement `IRecipeRepository` interface in `Core` and `RecipeRepository` (EF Core) in `Infrastructure`; implement `RecipeService` for business logic; register all with DI
- [ ] Build Recipe CRUD endpoints:
  - `GET /recipes` — paginated, with query params for search/filter
  - `GET /recipes/{id}`
  - `POST /recipes`
  - `PUT /recipes/{id}`
  - `DELETE /recipes/{id}`
  - Use FluentValidation for request validation
- [ ] Add search and filtering via query params: `?q=` (full-text LIKE search), `?tags=`, `?cuisine=`, `?maxTime=`; URL-encode friendly; plan for search index upgrade later

### React UI

- [ ] Build recipe list page with responsive card grid showing name, tags, cook time, and photo placeholder; use React Query for caching and background refetch
- [ ] Build recipe detail page with full ingredient list, step-by-step instructions, servings scaler (proportionally scales all ingredient quantities), tags, and print-friendly CSS
- [ ] Build recipe add/edit form with dynamic ingredient rows (add/remove), tag picker, cook time fields, and a markdown or rich text editor for instructions; use React Hook Form + Zod validation
- [ ] Build search bar and filter sidebar: debounced search input, tag filter chips, cuisine dropdown; sync all filter state to URL query params so search results are shareable

---

## Phase 3 · Users & Authentication

**Estimated time:** 1–2 weeks  
**Goal:** Family member accounts. Each user can register, log in, and have recipes scoped to them or shared with the whole family.

### Auth Infrastructure

- [ ] Add ASP.NET Core Identity with EF Core store; create `ApplicationUser : IdentityUser` with family-specific fields (display name, etc.); run migration to add identity tables
- [ ] Implement JWT authentication:
  - Short-lived access tokens (15 min)
  - Refresh tokens stored in DB (7–30 day expiry)
  - `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`
- [ ] Add authorization policies: `Admin` role (full CRUD on all recipes), `Member` role (CRUD own recipes + read shared recipes); add `UserId` FK to `Recipes`; filter queries by ownership

### Frontend Auth Flow

- [ ] Build login and register pages with form validation, API error display, and redirect to home on success; store access token in memory, refresh token in HttpOnly cookie
- [ ] Add React auth context for current user; add `PrivateRoute` wrapper that redirects unauthenticated users; add Axios interceptor to attach JWT header and auto-refresh on 401 response
- [ ] Build user profile and settings page: change display name, email, and password; view user's contributed recipes
- [ ] **Recommended:** Family invite system — admin generates an invite link/code; new users register via that code and join the family group; keeps the app private to your household

---

## Phase 4 · Shopping Lists

**Estimated time:** 1–2 weeks  
**Goal:** Generate a shopping list from one or more recipes. Consolidate duplicate ingredients, allow manual editing, and share the list across the family.

### Data Model

- [ ] Add shopping list schema and EF Core migration:
  - `ShoppingLists` (id, userId, name, createdAt)
  - `ShoppingListItems` (id, listId, ingredientName, quantity, unit, checked, recipeSource, sortOrder)

### Backend

- [ ] Build ingredient aggregation service: accepts a list of recipe IDs + serving multipliers; returns a deduplicated, merged ingredient list (e.g. 2× "1 cup flour" → "2 cups flour"); handles unit conversions for common cases
- [ ] Build shopping list endpoints:
  - `POST /shopping-lists/generate` — accepts recipe IDs + servings, returns a new list
  - Full CRUD for lists: `GET`, `POST`, `PUT`, `DELETE`
  - `PATCH /shopping-lists/{id}/items/{itemId}` — check/uncheck items, update quantity
  - `GET /shopping-lists/{id}/share` — generates a read-only token-based URL

### Frontend

- [ ] Add "Add to list" button on recipe detail pages: servings selector + dropdown to pick an existing list or create new; optimistic UI update via React Query mutation
- [ ] Build shopping list page: check off items with strikethrough animation, group items by category (produce, dairy, pantry, etc.), add manual items, clear checked items, delete list
- [ ] Implement shareable read-only list URL (accessible without login) — critical for grocery store mobile use

---

## Phase 5 · Meal Planning

**Estimated time:** 2 weeks  
**Goal:** A weekly planner where family members can slot recipes into days and meal slots, then generate a complete shopping list for the week.

### Data Model

- [ ] Add meal plan schema and EF Core migration:
  - `MealPlans` (id, userId, weekStartDate, name)
  - `MealPlanEntries` (id, planId, dayOfWeek [0–6], mealType [breakfast/lunch/dinner/snack], recipeId, servings)

### Backend

- [ ] Build meal plan endpoints:
  - `GET /meal-plans?week=2025-W22` — fetch plan for a given ISO week
  - `POST /meal-plans` — create a new plan for a week
  - `POST /meal-plans/{id}/entries` — add a recipe to a slot
  - `PUT /meal-plans/{id}/entries/{entryId}` — update servings or move slot
  - `DELETE /meal-plans/{id}/entries/{entryId}`
- [ ] Build `POST /meal-plans/{id}/shopping-list` — reuses Phase 4 aggregation service; aggregates all recipes in the plan into one shopping list

### Frontend

- [ ] Build weekly calendar grid: 7-column layout (Mon–Sun) with rows for each meal type; week navigation (previous/next); each slot shows recipe name with a remove button
- [ ] Add drag-and-drop recipe assignment using `@dnd-kit/core`: drag recipe cards from a sidebar into calendar slots; fallback — click a slot to open a recipe search modal
- [ ] Add "Generate shopping list" button for the whole week — creates a list and navigates the user to it immediately
- [ ] **Stretch goal:** Nutrition summary — if nutritional data exists on ingredients, show a weekly macro summary; can integrate USDA FoodData Central API (free) later

---

## Phase 6 · Polish & Scale

**Estimated time:** Ongoing — prioritize based on what the family uses most  
**Goal:** Elevate the experience, harden infrastructure, and add quality-of-life features once the core app is solid.

### Experience Upgrades

- [ ] **Photo uploads:** Upload to Azure Blob Storage or S3; store URL on recipe record; responsive image component with lazy loading and skeleton placeholder
- [ ] **PWA / mobile support:** Add Web App Manifest and service worker via Vite PWA plugin so the app installs on phones — critical for grocery store use of shopping lists
- [ ] **Ratings and notes:** Star rating per user, personal notes field on each recipe (e.g. "we added extra garlic") — scoped per user, not global
- [ ] **Recipe import from URL:** Scrape and parse `schema.org/Recipe` JSON-LD from external sites using HtmlAgilityPack; saves manual data entry for web recipes

### Infrastructure & Ops

- [ ] **Full-text search upgrade:** Add a SQL full-text index, or integrate Meilisearch or PostgreSQL full-text search (`tsvector`) to dramatically improve ingredient-based and multi-word search
- [ ] **Automated database backups:** Daily backup to cloud storage with tested restore procedure; if using Azure SQL or managed Postgres this is mostly configuration
- [ ] **Monitoring and error tracking:** Add Application Insights or Sentry for both backend and frontend; set up alerts for 5xx error spikes
- [ ] **E2E tests with Playwright:** Cover critical user journeys: add recipe → add to meal plan → generate shopping list → check off items; run in CI against a dedicated test database

---

## Key Milestones

| Milestone | Phase |
|---|---|
| Markdown file parsed and recipes visible in browser | 2 |
| Recipe search and filter working end-to-end | 2 |
| Family members can register, log in, and manage their own recipes | 3 |
| One-click shopping list from any recipe | 4 |
| Shareable list URL working on mobile | 4 |
| Drag-and-drop weekly meal planner live | 5 |
| One-click shopping list from an entire week's meal plan | 5 |

---

## Database Schema (Overview)

```
Users                       Recipes
─────────────────           ──────────────────────────
id (PK)                     id (PK)
email                       userId (FK → Users)
displayName                 title
passwordHash                description
role                        cuisine
createdAt                   servings
                            prepTimeMinutes
                            cookTimeMinutes
RecipeIngredients           instructions (text)
─────────────────           isShared (bool)
id (PK)                     createdAt / updatedAt
recipeId (FK)
ingredientId (FK)           Ingredients
quantity                    ──────────
unit                        id (PK)
notes                       name
                            normalizedName

Tags          RecipeTags
────          ──────────────
id (PK)       recipeId (FK)
name          tagId (FK)

ShoppingLists               ShoppingListItems
─────────────               ─────────────────
id (PK)                     id (PK)
userId (FK)                 listId (FK)
name                        ingredientName
shareToken                  quantity
createdAt                   unit
                            checked (bool)
                            recipeSource
                            sortOrder

MealPlans                   MealPlanEntries
─────────                   ───────────────
id (PK)                     id (PK)
userId (FK)                 planId (FK)
weekStartDate               recipeId (FK)
name                        dayOfWeek (0–6)
                            mealType (enum)
                            servings

UserRecipeNotes             UserRecipeRatings
───────────────             ─────────────────
id (PK)                     id (PK)
userId (FK)                 userId (FK)
recipeId (FK)               recipeId (FK)
notes (text)                rating (1–5)
updatedAt                   createdAt
```

---

## Notes

- **Markdown parser:** Invest time upfront auditing your markdown file's format before writing the parser. Define a strict spec (heading levels, ingredient line format, metadata fields) and write tests against real examples from your file. `Markdig` is the recommended C# markdown library.
- **Ingredient data structure:** Store ingredients as structured data from day one (name, quantity, and unit as separate columns) rather than raw strings. This is what makes the shopping list aggregation service in Phase 4 work correctly.
- **JWT storage:** Store access tokens in memory (not localStorage) and refresh tokens in HttpOnly cookies to reduce XSS risk.
- **Family privacy:** The invite link system in Phase 3 is optional but strongly recommended — without it, registration is either open (not ideal) or requires manual account creation.
- **Search:** Start with SQL LIKE queries. Only upgrade to a dedicated search index (Meilisearch, Postgres FTS) when you feel the pain — probably after 200+ recipes.
