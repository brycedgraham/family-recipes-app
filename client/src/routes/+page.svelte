<script>
  import { goto } from '$app/navigation';
  import { fetchRecipes, fetchCategories, getPhotoImportJobs } from '#lib/api.js';
  import CategoryList from '#lib/components/CategoryList.svelte';
  import RecipeCard from '#lib/components/RecipeCard.svelte';
  import ImportUrlModal from '#lib/components/ImportUrlModal.svelte';
  import PhotoImportModal from '#lib/components/PhotoImportModal.svelte';

  let categories = $state([]);
  let recipes = $state(null); // null while loading
  let selectedCategory = $state(null); // category name, or null for all
  let searchInput = $state('');
  let searchQuery = $state('');
  let importOpen = $state(false);
  let photoImportOpen = $state(false);
  let photoActiveCount = $state(0); // Pending + Processing jobs, shown as a badge on the sidebar button
  let refreshToken = $state(0); // bump to re-fetch the recipe list (e.g. a photo import completed)

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

  // One-shot badge bootstrap; the modal reports live counts back while it is open.
  getPhotoImportJobs().then(data => {
    photoActiveCount = (data.jobs ?? []).filter(j => j.status === 'Pending' || j.status === 'Processing').length;
  }).catch(() => { /* badge stays hidden until the modal opens */ });

  $effect(() => {
    let cancelled = false;
    recipes = null;
    refreshToken; // re-run when a photo import lands new recipes
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
    <button type="button" class="secondary" style="width: 100%; margin-top: 8px; text-align: center" onclick={() => (importOpen = true)}>Import from URL</button>
    <button type="button" class="secondary" style="width: 100%; margin-top: 8px; text-align: center" onclick={() => (photoImportOpen = true)}>Import from Photo{#if photoActiveCount > 0} ({photoActiveCount}){/if}</button>
    <a href="/api/backup" download class="secondary" style="display: block; width: 100%; margin-top: 8px">Download Backup</a>
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

  <ImportUrlModal
    open={importOpen}
    onClose={() => (importOpen = false)}
    onImported={recipe => {
      importOpen = false;
      goto(`/recipes/${recipe.id}`);
    }}
  />

  <PhotoImportModal
    open={photoImportOpen}
    onClose={() => (photoImportOpen = false)}
    onRecipesChanged={() => (refreshToken += 1)}
    onActiveChange={count => (photoActiveCount = count)}
  />
</div>
