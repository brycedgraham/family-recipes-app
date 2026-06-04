# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Global Rules

- **Ask before renaming or moving directories.** Never rename project folders without confirming with the user first.
- **Ask before making bulk structural changes.** If a task involves moving multiple files or restructuring the project, propose the plan first.
- **Prefer small, focused changes.** Edit one thing at a time rather than making sweeping transformations.

## Environment

- **Running in WSL2** (Ubuntu on Windows).
- **.NET 10 SDK is installed on the Windows side.** Run .NET commands via:
  ```bash
  powershell.exe -Command "dotnet <args>"
  ```
- **PostgreSQL** is available. Use `powershell.exe` for Windows-side database tools.
- **VS Code** is the primary editor.

## Project Architecture

Full-stack application: React SPA client + .NET 10 API backend.

### Client (`client/`)

React application built with Vite.

- **`client/src/components/`**: Main UI components (`CategoryList`, `RecipeCard`, `RecipeDetail`, `RecipeForm`, `RecipeList`)
- **`client/src/App.jsx`**: Main application component, managing state and routing

#### Client Commands

Run from `client/`:
- `npm run dev` — Start development server
- `npm run build` — Build for production
- `npm run lint` — Run linter

### API (`api/`)

.NET 10 solution with three projects in a layered architecture.

- **`api/RecipeApp.Core/`** — Domain models (`Recipe`, `Ingredient`, `RecipeIngredient`, `RecipeStep`, `Category`) and interfaces (`IRecipeRepository`)
- **`api/RecipeApp.Infrastructure/`** — EF Core `RecipeDbContext` and `RecipeRepository` implementation (PostgreSQL via Npgsql)
- **`api/RecipeApp/`** — ASP.NET Core Web API host with DI wiring

#### API Commands

Run from `api/`:
- `dotnet restore` — Restore NuGet packages
- `dotnet build` — Build solution
- `dotnet run` — Run the API server

### Data

- **`recipes_json/`** — Raw recipe JSON files extracted from `Recipes.md`
- **`extract_recipes.py`** — Script to parse `Recipes.md` into JSON

## Data Models

The application revolves around five primary entities:
- **`Recipe`**: Title, description, cuisine, servings, prep/cook time, timestamps
- **`Ingredient`**: Name (with normalized form for dedup), category (produce, dairy, pantry, etc.)
- **`RecipeIngredient`**: Bridge table — quantity, unit, notes per recipe-ingredient pair
- **`RecipeStep`**: Ordered instruction steps per recipe
- **`Category`**: Named group of recipes (many-to-many via `RecipeCategories` join table)
