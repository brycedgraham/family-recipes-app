# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

All commands should be run from the `recipe-website` directory.

- **Start development server**: `npm run dev`
- **Build application**: `npm run build`
- **Run linter**: `npm run lint`
- **Preview build**: `npm run preview`

## Project Architecture

This is a React application built with Vite.

### Core Structure

- **`src/components/`**: Contains the main UI components:
    - `CategoryList.jsx`: Displays available recipe categories.
    - `RecipeCard.jsx`: A summary card for an individual recipe.
    - `RecipeDetail.jsx`: Detailed view of a specific recipe, including ingredients and instructions.
    - `RecipeForm.jsx`: Form for adding or editing recipes.
    - `RecipeList.jsx`: Displays a list of recipes, often filtered by category.
- **`src/dataModels.js`**: Defines the core data structures (`Recipe`, `Ingredient`, `Category`) using JSDoc.
- **`src/mockData.js`**: Provides initial mock data for development and testing.
- **`src/App.jsx`**: The main application component, managing state and routing.

### Data Models

The application revolves around three primary entities:
- **`Recipe`**: Includes name, description, categories, ingredients, instructions, and metadata like prep/cook time.
- **`Ingredient`**: Contains name and quantity.
- **`Category`**: Represents a group of recipes.
