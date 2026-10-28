<script>
  let { recipe } = $props();
</script>

<div class="RecipeCard">
  <a href="/recipes/{recipe.id}" style="text-decoration: none; color: inherit; display: contents">
    {#if recipe.imageUrls && recipe.imageUrls.length > 0}
      <img src={recipe.imageUrls[0]} alt={recipe.name} />
    {:else}
      <div class="no-image" aria-hidden="true">🍲</div>
    {/if}
    <h3>{recipe.name}</h3>
  </a>
  <p class="recipe-card-description">{recipe.description}</p>
  <p class="recipe-card-categories">
    <small>Categories: {recipe.categories.join(', ')}</small>
  </p>
  <a href="/recipes/{recipe.id}" class="button-link">View Recipe</a>
</div>
