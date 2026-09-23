import React, { useState, useEffect } from 'react';
import { Routes, Route, useParams, Link, useNavigate } from 'react-router-dom';
import CategoryList from './components/CategoryList';
import RecipeList from './components/RecipeList';
import RecipeDetail from './components/RecipeDetail';
import RecipeForm from './components/RecipeForm';
import { fetchRecipes, fetchCategories, fetchRecipe, deleteRecipe } from './api';
import './App.css';

// Main layout: category sidebar + recipe list, all data from the API.
function HomeLayout() {
  const [categories, setCategories] = useState([]);
  const [recipes, setRecipes] = useState(null); // null while loading
  const [selectedCategory, setSelectedCategory] = useState(null); // category name, or null for all
  const [searchInput, setSearchInput] = useState('');
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    fetchCategories()
      .then(setCategories)
      .catch(err => console.error('Failed to load categories:', err));
  }, []);

  // Debounce the search box so we don't hit the API on every keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setSearchQuery(searchInput.trim()), 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    let cancelled = false;
    setRecipes(null);
    fetchRecipes({ q: searchQuery || undefined, category: selectedCategory })
      .then(data => { if (!cancelled) setRecipes(data); })
      .catch(err => { console.error('Failed to load recipes:', err); if (!cancelled) setRecipes([]); });
    return () => { cancelled = true; };
  }, [searchQuery, selectedCategory]);

  return (
    <div className="App"> {/* Use class from App.css */}
      <aside className="App-sidebar"> {/* Use class from App.css */}
        <h2>
          <Link to="/" onClick={() => setSelectedCategory(null)}>Recipe App</Link>
        </h2>
        <CategoryList
          categories={categories}
          onSelectCategory={setSelectedCategory}
          selectedCategory={selectedCategory}
        />
        <hr style={{margin: "20px 0"}}/>
        <Link to="/recipes/new" className="button-link" style={{textAlign: 'center'}}>
          Add New Recipe
        </Link>
      </aside>
      <main className="App-main-content"> {/* Use class from App.css */}
        <input
          type="search"
          placeholder="Search recipes…"
          value={searchInput}
          onChange={e => setSearchInput(e.target.value)}
          style={{width: '100%', marginBottom: '15px', padding: '6px'}}
        />
        {recipes === null ? (
          <p>Loading recipes…</p>
        ) : (
          <RecipeList recipes={recipes} />
        )}
      </main>
    </div>
  );
}

// Component to display a single recipe's details, fetched by id.
function RecipePage() {
  const { recipeId } = useParams();
  const navigate = useNavigate();
  const [recipe, setRecipe] = useState(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetchRecipe(recipeId)
      .then(data => { if (!cancelled) setRecipe(data); })
      .catch(err => {
        console.error('Failed to load recipe:', err);
        if (!cancelled) setNotFound(true);
      });
    return () => { cancelled = true; };
  }, [recipeId]);

  const handleDelete = async () => {
    if (!window.confirm(`Delete "${recipe.name}"? This cannot be undone.`)) return;
    try {
      await deleteRecipe(recipe.id);
      navigate('/');
    } catch (err) {
      alert(`Delete failed: ${err.message}`);
    }
  };

  if (notFound) {
    return (
      <div className="container text-center mt-3">
        <p>Recipe not found.</p>
        <button onClick={() => navigate('/')} className="mt-2">Go Home</button>
      </div>
    );
  }

  if (!recipe) {
    return (
      <div className="App-main-content">
        <p>Loading recipe…</p>
      </div>
    );
  }

  return (
    <div className="App-main-content"> {/* Use class from App.css */}
      <nav className="mb-3">
        <button onClick={() => navigate(-1)} className="mr-2 secondary">&larr; Back</button>
        <Link to={`/recipes/${recipe.id}/edit`} className="button-link">
          Edit Recipe
        </Link>
        <button onClick={handleDelete} className="danger ml-2">Delete</button>
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
