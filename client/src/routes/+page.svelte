<script>
  import { fetchRecipes, fetchCategories } from '#lib/api.js';
  import CategoryList from '#lib/components/CategoryList.svelte';
  import RecipeCard from '#lib/components/RecipeCard.svelte';

  let categories = $state([]);
  let recipes = $state(null); // null while loading
  let selectedCategory = $state(null); // category name, or null for all
  let searchInput = $state('');
  let searchQuery = $state('');

  fetchCategories().then(data => {
    categories = data;
  }).catch(err => console.error('Failed to load categories:', err));

  // Debounce the search box so we don't hit the API on every keystroke.
  $effect(() => {
    const query = searchInput.trim(); // read synchronously so the effect re-runs on every keystroke
    const timer = setTimeout(() => {
      searchQuery = query;
    }, 300);
    return () => clearTimeout(timer);
  });

  $effect(() => {
    let cancelled = false;
    recipes = null;
    fetchRecipes({ q: searchQuery || undefined, category: selectedCategory })
      .then(data => { if (!cancelled) recipes = data; })
      .catch(err => { console.error('Failed to load recipes:', err); if (!cancelled) recipes = []; });
    return () => { cancelled = true; };
  });
</script>

<div class="App">
  <aside class="App-sidebar">
    <div class="App-sidebar-header">
      <h2><a href="/">Recipe App</a></h2>
    </div>
    <CategoryList
      {categories}
      {selectedCategory}
      onSelect={c => (selectedCategory = c)}
    />
    <hr style="margin: 20px 0" />
    <a href="/recipes/new" class="button-link" style="text-align: center">Add New Recipe</a>
  </aside>
  <main class="App-main-content">
    <input
      type="search"
      placeholder="Search recipes…"
      bind:value={searchInput}
      style="width: 100%; margin-bottom: 15px; padding: 6px"
    />
    {#if recipes === null}
      <p>Loading recipes…</p>
    {:else if recipes.length === 0}
      <p class="text-center mt-3">No recipes found for this selection.</p>
    {:else}
      <div class="RecipeList">
        {#each recipes as recipe (recipe.id)}
          <RecipeCard {recipe} />
        {/each}
      </div>
    {/if}
  </main>
</div>
