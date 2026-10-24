<script>
  import { fetchShoppingLists, createShoppingList, addRecipesToShoppingList, addMealPlanEntry, uploadRecipeImage, deleteRecipeImage } from '#lib/api.js';
  import { formatQuantity } from '#lib/format.js';
  import { mondayOf, todayISO, toISO } from '#lib/dates.js';

  const SCALE_OPTIONS = [0.5, 1, 2, 3];
  const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const MEAL_TYPES = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

  let { recipe } = $props();

  let scale = $state(1);
  let adding = $state(false);
  let addedMsg = $state(null);

  // Photo gallery: local override so the UI updates right after an upload/delete without a refetch.
  let imageUrlsOverride = $state(undefined);
  let imageUrls = $derived(imageUrlsOverride !== undefined ? imageUrlsOverride : (recipe.imageUrls ?? []));
  let activeImage = $state(0);
  let uploading = $state(false);
  let imgMsg = $state(null);
  let fileInput;

  function fileNameFromUrl(url) {
    return url.split('/').pop();
  }

  async function handleUpload(event) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = ''; // allow re-selecting the same file later
    if (files.length === 0) return;
    uploading = true;
    imgMsg = null;
    try {
      let updated = recipe;
      for (const file of files) updated = await uploadRecipeImage(recipe.id, file);
      imageUrlsOverride = updated.imageUrls ?? [];
      activeImage = Math.max(0, imageUrlsOverride.length - files.length); // jump to the first new photo
      imgMsg = `Added ${files.length} photo${files.length > 1 ? 's' : ''}`;
    } catch (err) {
      imgMsg = `Upload failed: ${err.message}`;
    } finally {
      uploading = false;
    }
  }

  async function handleRemove(url) {
    try {
      const updated = await deleteRecipeImage(recipe.id, fileNameFromUrl(url));
      imageUrlsOverride = updated.imageUrls ?? [];
      activeImage = Math.min(activeImage, Math.max(0, imageUrlsOverride.length - 1));
      imgMsg = 'Photo removed';
    } catch (err) {
      alert(`Remove failed: ${err.message}`);
    }
  }

  // "Add to Meal Plan" popover state (adds to the current week).
  let planOpen = $state(false);
  let planSaving = $state(false);
  let planMsg = $state(null);
  let planForm = $state({ dayOfWeek: 0, mealType: 'Dinner', multiplier: 1 });

  // Scales the displayed quantities; "Add to Shopping List" sends the same multiplier to the API.
  function displayQty(ing) {
    return ing.amount !== null && ing.amount !== undefined
      ? formatQuantity(ing.amount * scale, ing.unit)
      : ing.quantity;
  }

  async function handleAddToList() {
    adding = true;
    addedMsg = null;
    try {
      let lists = await fetchShoppingLists();
      if (lists.length === 0) lists = [await createShoppingList({})]; // auto-create a default list
      const target = lists[0];
      await addRecipesToShoppingList(target.id, [{ recipeId: recipe.id, multiplier: scale }]);
      addedMsg = `Added to "${target.name}" at ${scale}x`;
    } catch (err) {
      alert(`Add to shopping list failed: ${err.message}`);
    } finally {
      adding = false;
    }
  }

  function openPlan() {
    planOpen = !planOpen;
    planMsg = null;
    if (planOpen) planForm = { dayOfWeek: 0, mealType: 'Dinner', multiplier: scale }; // prefill with current scale
  }

  async function handleAddToPlan(event) {
    event.preventDefault();
    planSaving = true;
    planMsg = null;
    try {
      await addMealPlanEntry(toISO(mondayOf(todayISO())), {
        dayOfWeek: Number(planForm.dayOfWeek),
        mealType: planForm.mealType,
        recipeId: recipe.id,
        multiplier: Number(planForm.multiplier) || 1,
      });
      planMsg = `Added to ${DAYS[Number(planForm.dayOfWeek)]} ${planForm.mealType}`;
    } catch (err) {
      planMsg = `Add failed: ${err.message}`;
    } finally {
      planSaving = false;
    }
  }
</script>

<div class="RecipeDetail">
  <h1>{recipe.name}</h1>

  {#if imageUrls.length > 0}
    <div class="recipe-gallery mb-3">
      <div class="gallery-hero">
        <img src={imageUrls[activeImage]} alt="{recipe.name} - photo {activeImage + 1}" />
        <button type="button" class="gallery-x" aria-label="Remove current photo" onclick={() => handleRemove(imageUrls[activeImage])}>&times;</button>
      </div>
      {#if imageUrls.length > 1}
        <div class="gallery-thumbs">
          {#each imageUrls as url, index (url)}
            <span class="thumb-wrap">
              <button type="button" class="thumb-img" class:active={index === activeImage} aria-label="Show photo {index + 1}" onclick={() => (activeImage = index)}>
                <img src={url} alt="" />
              </button>
              <button type="button" class="thumb-x" aria-label="Remove photo {index + 1}" onclick={() => handleRemove(url)}>&times;</button>
            </span>
          {/each}
        </div>
      {/if}
    </div>
  {:else}
    <div class="gallery-empty mb-3">No photos yet.</div>
  {/if}

  <div class="recipe-photo-actions mb-2">
    <button type="button" class="secondary" disabled={uploading} onclick={() => fileInput?.click()}>
      {uploading ? 'Uploading…' : 'Upload Photo'}
    </button>
    <input bind:this={fileInput} type="file" accept="image/*" multiple hidden onchange={handleUpload} />
    {#if imgMsg}<span class="sl-added">{imgMsg}</span>{/if}
  </div>

  <p><strong>Description:</strong> {recipe.description}</p>
  <p class="mb-2"><strong>Categories:</strong> {recipe.categories.join(', ')}</p>

  {#if recipe.prepTime || recipe.cookTime || recipe.servings}
    <div class="recipe-meta mb-2" style="display: flex; gap: 20px; flex-wrap: wrap">
      {#if recipe.prepTime}<p><strong>Prep Time:</strong> {recipe.prepTime} minutes</p>{/if}
      {#if recipe.cookTime}<p><strong>Cook Time:</strong> {recipe.cookTime} minutes</p>{/if}
      {#if recipe.servings}<p><strong>Servings:</strong> {recipe.servings}</p>{/if}
    </div>
  {/if}

  <h2>Ingredients</h2>
  <div class="recipe-scale-row mb-2">
    <div class="scale-buttons">
      {#each SCALE_OPTIONS as s (s)}
        <button class:active={s === scale} onclick={() => (scale = s)}>{s}x</button>
      {/each}
    </div>
    <button onclick={handleAddToList} disabled={adding}>{adding ? 'Adding…' : 'Add to Shopping List'}</button>
    {#if addedMsg}<span class="sl-added">{addedMsg}</span>{/if}
  </div>
  <div class="recipe-scale-row mb-2">
    <button class="secondary" onclick={openPlan}>Add to Meal Plan</button>
    {#if planMsg}<span class="sl-added">{planMsg}</span>{/if}
  </div>
  {#if planOpen}
    <form class="mp-inline-form mb-2" onsubmit={handleAddToPlan}>
      <label>Day
        <select bind:value={planForm.dayOfWeek}>
          {#each DAYS as dayName, i (dayName)}
            <option value={i}>{dayName}</option>
          {/each}
        </select>
      </label>
      <label>Meal
        <select bind:value={planForm.mealType}>
          {#each MEAL_TYPES as t (t)}
            <option value={t}>{t}</option>
          {/each}
        </select>
      </label>
      <label>Qty
        <input type="number" min="0.5" step="0.5" bind:value={planForm.multiplier} />
      </label>
      <button type="submit" disabled={planSaving}>{planSaving ? 'Adding…' : 'Add'}</button>
    </form>
  {/if}
  {#if recipe.ingredients.length === 0}
    <p>No ingredients listed.</p>
  {:else}
    <ul>
      {#each recipe.ingredients as ingredient, index (index)}
        <li>
          {[displayQty(ingredient), ingredient.name].filter(Boolean).join(' ')}
        </li>
      {/each}
    </ul>
  {/if}

  <h2>Instructions</h2>
  {#if recipe.instructions.length === 0}
    <p>No instructions listed.</p>
  {:else}
    <ol>
      {#each recipe.instructions as step, index (index)}
        <li class="mb-1">{step}</li>
      {/each}
    </ol>
  {/if}
</div>
