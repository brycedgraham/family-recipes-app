import React from 'react';

/**
 * @param {{ recipe: import('../dataModels').Recipe }} props
 */
function RecipeDetail({ recipe }) {
  // Note: The 'recipe not found' case is now handled in RecipePage component in App.jsx
  // So, we can assume 'recipe' prop is always provided here.

  return (
    <div className="RecipeDetail"> {/* Use class from App.css */}
      <h1>{recipe.name}</h1>

      {recipe.imageUrls && recipe.imageUrls.length > 0 && (
        <div className="recipe-images mb-3">
          {recipe.imageUrls.map((url, index) => (
            <img
              key={index}
              src={url}
              alt={`${recipe.name} - image ${index + 1}`}
              className="mb-2" // Styles for img are in App.css under .RecipeDetail img
            />
          ))}
        </div>
      )}

      <p><strong>Description:</strong> {recipe.description}</p>
      <p className="mb-2"><strong>Categories:</strong> {recipe.categories.join(', ')}</p>

      {(recipe.prepTime || recipe.cookTime || recipe.servings) && (
        <div className="recipe-meta mb-2" style={{display: "flex", gap: "20px", flexWrap: "wrap"}}>
          {recipe.prepTime && <p><strong>Prep Time:</strong> {recipe.prepTime} minutes</p>}
          {recipe.cookTime && <p><strong>Cook Time:</strong> {recipe.cookTime} minutes</p>}
          {recipe.servings && <p><strong>Servings:</strong> {recipe.servings}</p>}
        </div>
      )}

      <h2>Ingredients</h2>
      <ul>
        {recipe.ingredients.map((ingredient, index) => (
          <li key={index}>
            {ingredient.quantity} {ingredient.name}
          </li>
        ))}
      </ul>

      <h2>Instructions</h2>
      <ol>
        {recipe.instructions.map((step, index) => (
          <li key={index} className="mb-1">{step}</li>
        ))}
      </ol>
    </div>
  );
}

export default RecipeDetail;
