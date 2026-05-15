import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';

// Assuming mockRecipes is accessible for pre-filling the form in edit mode.
// In a real app, you'd fetch this data.
import { mockRecipes } from '../mockData'; // Updated import for mock data

/**
 * @param {{ isEditMode?: boolean }} props
 */
function RecipeForm({ isEditMode = false }) {
  const navigate = useNavigate();
  const { recipeId } = useParams();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [categories, setCategories] = useState(''); // Comma-separated string for simplicity
  const [ingredients, setIngredients] = useState([{ name: '', quantity: '' }]); // Array of objects
  const [instructions, setInstructions] = useState(''); // Comma-separated string or one per line
  const [imageUrls, setImageUrls] = useState(''); // Comma-separated string
  const [prepTime, setPrepTime] = useState('');
  const [cookTime, setCookTime] = useState('');
  const [servings, setServings] = useState('');

  useEffect(() => {
    if (isEditMode && recipeId) {
      const recipeToEdit = mockRecipes.find(r => r.id === recipeId);
      if (recipeToEdit) {
        setName(recipeToEdit.name);
        setDescription(recipeToEdit.description);
        setCategories(recipeToEdit.categories.join(', '));
        setIngredients(recipeToEdit.ingredients.map(ing => ({ ...ing }))); // Shallow copy ingredients
        setInstructions(recipeToEdit.instructions.join('\n'));
        setImageUrls(recipeToEdit.imageUrls.join(', '));
        setPrepTime(recipeToEdit.prepTime?.toString() || '');
        setCookTime(recipeToEdit.cookTime?.toString() || '');
        setServings(recipeToEdit.servings?.toString() || '');
      } else {
        // Handle recipe not found for editing, maybe navigate away or show error
        navigate('/');
      }
    } else { // Reset form for "Add New" mode or if navigating away from edit
      setName('');
      setDescription('');
      setCategories('');
      setIngredients([{ name: '', quantity: '' }]);
      setInstructions('');
      setImageUrls('');
      setPrepTime('');
      setCookTime('');
      setServings('');
    }
  }, [isEditMode, recipeId, navigate]);

  const handleIngredientChange = (index, field, value) => {
    const newIngredients = [...ingredients];
    newIngredients[index][field] = value;
    setIngredients(newIngredients);
  };

  const addIngredientField = () => {
    setIngredients([...ingredients, { name: '', quantity: '' }]);
  };

  const removeIngredientField = (index) => {
    const newIngredients = ingredients.filter((_, i) => i !== index);
    setIngredients(newIngredients);
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    // Placeholder for actual submission logic
    const recipeData = {
      name,
      description,
      categories: categories.split(',').map(cat => cat.trim()),
      ingredients, // In a real app, ensure ingredients are structured correctly
      instructions: instructions.split('\n').map(inst => inst.trim()),
      imageUrls: imageUrls.split(',').map(url => url.trim()),
      prepTime: prepTime ? parseInt(prepTime) : undefined,
      cookTime: cookTime ? parseInt(cookTime) : undefined,
      servings: servings ? parseInt(servings) : undefined,
    };
    console.log('Submitting recipe (placeholder):', recipeData);
    alert(`Recipe data (placeholder): ${isEditMode ? 'Updated' : 'Created'}\n${JSON.stringify(recipeData, null, 2)}`);
    navigate(isEditMode ? `/recipes/${recipeId}` : '/'); // Navigate after mock submission
  };

  return (
    // Use className from App.css for RecipeForm
    // The parent element that renders this might need App-main-content if it's not already there.
    // Assuming RecipeForm itself is the main content for these routes.
    <div className="App-main-content">
      <form onSubmit={handleSubmit} className="RecipeForm">
        <h2>{isEditMode ? 'Edit Recipe' : 'Add New Recipe'}</h2>

        <div>
          <label htmlFor="name">Name:</label>
          <input type="text" id="name" value={name} onChange={e => setName(e.target.value)} required />
        </div>

        <div>
          <label htmlFor="description">Description:</label>
          <textarea id="description" value={description} onChange={e => setDescription(e.target.value)} rows="3" />
        </div>

        <div>
          <label htmlFor="categories">Categories (comma-separated):</label>
          <input type="text" id="categories" value={categories} onChange={e => setCategories(e.target.value)} />
        </div>

        <div>
          <label>Ingredients:</label>
          {ingredients.map((ing, index) => (
            <div key={index} className="ingredient-field"> {/* Class for styling */}
              <input
                type="text"
                placeholder="Quantity"
                value={ing.quantity}
                onChange={e => handleIngredientChange(index, 'quantity', e.target.value)}
              />
              <input
                type="text"
                placeholder="Name"
                value={ing.name}
                onChange={e => handleIngredientChange(index, 'name', e.target.value)}
              />
              {ingredients.length > 1 &&
                <button type="button" className="danger" onClick={() => removeIngredientField(index)}>Remove</button>}
            </div>
          ))}
          <button type="button" onClick={addIngredientField} className="secondary mb-2">Add Ingredient</button>
        </div>

        <div>
          <label htmlFor="instructions">Instructions (one step per line):</label>
          <textarea id="instructions" value={instructions} onChange={e => setInstructions(e.target.value)} rows="5" />
        </div>

        <div>
          <label htmlFor="imageUrls">Image URLs (comma-separated):</label>
          <input type="text" id="imageUrls" value={imageUrls} onChange={e => setImageUrls(e.target.value)} />
        </div>

        <div style={{display: "flex", gap: "15px", flexWrap: "wrap"}}>
          <div style={{flexGrow:1, minWidth: "120px"}}>
              <label htmlFor="prepTime">Prep Time (mins):</label>
              <input type="number" id="prepTime" value={prepTime} onChange={e => setPrepTime(e.target.value)} />
          </div>
          <div style={{flexGrow:1, minWidth: "120px"}}>
              <label htmlFor="cookTime">Cook Time (mins):</label>
              <input type="number" id="cookTime" value={cookTime} onChange={e => setCookTime(e.target.value)} />
          </div>
          <div style={{flexGrow:1, minWidth: "120px"}}>
              <label htmlFor="servings">Servings:</label>
              <input type="number" id="servings" value={servings} onChange={e => setServings(e.target.value)} />
          </div>
        </div>

        <div className="form-actions"> {/* Class for styling */}
          <button type="submit">{isEditMode ? 'Save Changes' : 'Add Recipe'}</button>
          <button type="button" className="secondary" onClick={() => navigate(isEditMode ? `/recipes/${recipeId}` : '/')}>Cancel</button>
        </div>
      </form>
    </div>
  );
}

export default RecipeForm;
