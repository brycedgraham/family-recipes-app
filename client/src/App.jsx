import React, { useState, useEffect } from 'react';
import { Routes, Route, useParams, Link, useNavigate } from 'react-router-dom';
import CategoryList from './components/CategoryList';
import RecipeList from './components/RecipeList';
import RecipeDetail from './components/RecipeDetail';
import RecipeForm from './components/RecipeForm'; // Import the new form
import { mockCategories, mockRecipes } from './mockData'; // Import mock data
import './App.css';

// Component to handle the main layout with categories and recipe list
function HomeLayout() {
  const [selectedCategoryId, setSelectedCategoryId] = useState(null);
  const [recipesToDisplay, setRecipesToDisplay] = useState(mockRecipes);
  // const navigate = useNavigate(); // Not strictly needed here anymore for category nav

  useEffect(() => {
    if (selectedCategoryId === null) {
      setRecipesToDisplay(mockRecipes);
    } else {
      const selectedCategory = mockCategories.find(cat => cat.id === selectedCategoryId);
      if (selectedCategory) {
        setRecipesToDisplay(
          mockRecipes.filter(recipe => recipe.categories.includes(selectedCategory.name))
        );
      } else {
        setRecipesToDisplay([]);
      }
    }
  }, [selectedCategoryId]);

  const handleSelectCategory = (categoryId) => {
    setSelectedCategoryId(categoryId);
  };

  return (
    <div className="App"> {/* Use class from App.css */}
      <aside className="App-sidebar"> {/* Use class from App.css */}
        <h2>
          <Link to="/" onClick={() => handleSelectCategory(null)}>Recipe App</Link>
        </h2>
        <CategoryList
          categories={mockCategories}
          onSelectCategory={handleSelectCategory}
          selectedCategoryId={selectedCategoryId}
        />
        <hr style={{margin: "20px 0"}}/>
        <Link to="/recipes/new" className="button-link" style={{textAlign: 'center'}}>
          Add New Recipe
        </Link>
      </aside>
      <main className="App-main-content"> {/* Use class from App.css */}
        <RecipeList recipes={recipesToDisplay} />
      </main>
    </div>
  );
}

// Component to display a single recipe's details
function RecipePage() {
  const { recipeId } = useParams();
  const recipe = mockRecipes.find(r => r.id === recipeId);
  const navigate = useNavigate();

  if (!recipe) {
    return (
      <div className="container text-center mt-3">
        <p>Recipe not found.</p>
        <button onClick={() => navigate('/')} className="mt-2">Go Home</button>
      </div>
    );
  }

  return (
    <div className="App-main-content"> {/* RecipePage now also uses App-main-content for consistent padding/scroll */}
      <nav className="mb-3">
        <button onClick={() => navigate(-1)} className="mr-2 secondary">← Back</button>
        <Link to={`/recipes/${recipeId}/edit`} className="button-link">
          Edit Recipe
        </Link>
      </nav>
      <RecipeDetail recipe={recipe} />
    </div>
  );
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<HomeLayout />} />
      <Route path="/recipes/new" element={<RecipeForm />} /> {/* Add new recipe route */}
      <Route path="/recipes/:recipeId" element={<RecipePage />} />
      <Route path="/recipes/:recipeId/edit" element={<RecipeForm isEditMode={true} />} /> {/* Edit recipe route */}
    </Routes>
  );
}

export default App;
