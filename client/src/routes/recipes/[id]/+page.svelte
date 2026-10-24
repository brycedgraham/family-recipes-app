<script>
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { fetchRecipe, deleteRecipe } from '#lib/api.js';
  import RecipeDetail from '#lib/components/RecipeDetail.svelte';

  let recipe = $state(null);
  let notFound = $state(false);

  $effect(() => {
    const id = page.params.id;
    let cancelled = false;
    fetchRecipe(id)
      .then(data => { if (!cancelled) recipe = data; })
      .catch(err => {
        console.error('Failed to load recipe:', err);
        if (!cancelled) notFound = true;
      });
    return () => { cancelled = true; };
  });

  async function handleDelete() {
    if (!window.confirm(`Delete "${recipe.name}"? This cannot be undone.`)) return;
    try {
      await deleteRecipe(recipe.id);
      goto('/');
    } catch (err) {
      alert(`Delete failed: ${err.message}`);
    }
  }
</script>

{#if notFound}
  <div class="container text-center mt-3">
    <p>Recipe not found.</p>
    <a href="/" class="mt-2">Go Home</a>
  </div>
{:else if !recipe}
  <div class="App-main-content"><p>Loading recipe…</p></div>
{:else}
  <div class="App-main-content">
    <nav class="mb-3 RecipePage-nav">
      <button onclick={() => history.back()} class="mr-2 secondary">&larr; Back</button>
      <a href="/recipes/{recipe.id}/edit" class="button-link">Edit Recipe</a>
      <button onclick={handleDelete} class="danger ml-2">Delete</button>
    </nav>
    <RecipeDetail {recipe} />
  </div>
{/if}
