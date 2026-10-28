<script>
  let { categories = [], selectedCategory = null, onSelect } = $props();
</script>

<div class="CategoryList">
  <h3 style="margin-top: 15px; margin-bottom: 10px">Categories</h3>
  <ul>
    <li>
      <button type="button" class="category-item" class:active-category={selectedCategory === null} onclick={() => onSelect(null)}>All Recipes</button>
    </li>
    {#each categories as category (category.id)}
      <li>
        <button type="button" class="category-item" class:active-category={selectedCategory === category.name} onclick={() => onSelect(category.name)}>
          {category.name}
        </button>
      </li>
    {/each}
  </ul>
</div>
