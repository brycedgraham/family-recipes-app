import React from 'react';
import RecipeCard from './RecipeCard';

/**
 * @param {{ recipes: import('../dataModels').Recipe[] }} props
 */
function RecipeList({ recipes }) {
  if (!recipes || recipes.length === 0) {
    return <p className="text-center mt-3">No recipes found for this selection.</p>;
  }

  return (
    <div className="RecipeList"> {/* Use class from App.css */}
      {recipes.map(recipe => (
        <RecipeCard key={recipe.id} recipe={recipe} />
      ))}
    </div>
  );
}

export default RecipeList;
