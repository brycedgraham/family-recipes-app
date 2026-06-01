import React from 'react';
import { Link } from 'react-router-dom';

/**
 * @param {{ recipe: import('../dataModels').Recipe }} props
 */
function RecipeCard({ recipe }) {
  return (
    <div className="RecipeCard"> {/* Use class from App.css */}
      <Link to={`/recipes/${recipe.id}`} style={{ textDecoration: 'none', color: 'inherit', display: 'contents' }}>
        {recipe.imageUrls && recipe.imageUrls.length > 0 && (
          <img
            src={recipe.imageUrls[0]}
            alt={recipe.name}
            // Styles for img are now in App.css under .RecipeCard img
          />
        )}
        <h3>{recipe.name}</h3>
      </Link>
      <p className="recipe-card-description">{recipe.description}</p>
      <p className="recipe-card-categories">
        <small>Categories: {recipe.categories.join(', ')}</small>
      </p>
      <Link to={`/recipes/${recipe.id}`} className="button-link">
        View Recipe
      </Link>
    </div>
  );
}

export default RecipeCard;
