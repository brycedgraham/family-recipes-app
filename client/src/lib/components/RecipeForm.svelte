<script>
  import { goto } from '$app/navigation';
  import { fetchRecipe, createRecipe, updateRecipe, uploadRecipeImage, deleteRecipeImage } from '#lib/api.js';

  let { isEditMode = false, recipeId = null } = $props();

  let name = $state('');
  let description = $state('');
  let categories = $state(''); // comma-separated string for simplicity
  let ingredients = $state([{ name: '', quantity: '' }]); // array of objects
  let instructions = $state(''); // one step per line
  let prepTime = $state('');
  let cookTime = $state('');
  let servings = $state('');
  let submitting = $state(false);

  // Photos: files picked for upload + existing recipe photos (edit mode).
  let photos = $state([]);
  let photoPreviews = $state([]);
  let existingImages = $state([]);

  $effect(() => {
    const urls = photos.map((f) => URL.createObjectURL(f)); // synchronous read of `photos` keeps the effect reactive
    photoPreviews = urls;
    return () => urls.forEach((u) => URL.revokeObjectURL(u));
  });

  $effect(() => {
    if (!isEditMode || !recipeId) return;
    let cancelled = false;
    fetchRecipe(recipeId)
      .then((recipeToEdit) => {
        if (cancelled) return;
        name = recipeToEdit.name;
        description = recipeToEdit.description || '';
        categories = (recipeToEdit.categories || []).join(', ');
        ingredients = (recipeToEdit.ingredients || []).map(ing => ({ ...ing }));
        instructions = (recipeToEdit.instructions || []).join('\n');
        prepTime = recipeToEdit.prepTime?.toString() || '';
        cookTime = recipeToEdit.cookTime?.toString() || '';
        servings = recipeToEdit.servings?.toString() || '';
        existingImages = recipeToEdit.imageUrls ?? [];
      })
      .catch((err) => {
        console.error('Failed to load recipe for editing:', err);
        if (!cancelled) goto('/');
      });
    return () => { cancelled = true; };
  });

  function addIngredientField() {
    ingredients.push({ name: '', quantity: '' });
  }

  function removeIngredientField(index) {
    ingredients = ingredients.filter((_, i) => i !== index);
  }

  function handlePhotosChange(event) {
    photos = Array.from(event.target.files ?? []);
    event.target.value = ''; // allow re-selecting the same files later
  }

  function removePhoto(index) {
    photos = photos.filter((_, i) => i !== index);
  }

  async function removeExistingImage(url) {
    try {
      const updated = await deleteRecipeImage(recipeId, url.split('/').pop());
      existingImages = updated.imageUrls ?? [];
    } catch (err) {
      alert(`Remove failed: ${err.message}`);
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();
    const payload = {
      name,
      description: description.trim() || null,
      categories: categories.split(',').map(cat => cat.trim()).filter(Boolean),
      ingredients: ingredients
        .filter(ing => ing.name.trim())
        .map(ing => ({ name: ing.name.trim(), quantity: ing.quantity?.trim() || null })),
      instructions: instructions.split('\n').map(inst => inst.trim()).filter(Boolean),
      prepTime: prepTime ? parseInt(prepTime, 10) : null,
      cookTime: cookTime ? parseInt(cookTime, 10) : null,
      servings: servings ? parseInt(servings, 10) : null,
    };
    submitting = true;
    try {
      const saved = isEditMode ? await updateRecipe(recipeId, payload) : await createRecipe(payload);
      if (photos.length > 0) {
        try {
          for (const file of photos) await uploadRecipeImage(saved.id, file);
        } catch (err) {
          alert(`Recipe saved, but photo upload failed: ${err.message}`);
        }
      }
      goto(`/recipes/${saved.id}`);
    } catch (err) {
      console.error('Failed to save recipe:', err);
      alert(`Failed to save recipe: ${err.message}`);
      submitting = false;
    }
  }
</script>

<div class="App-main-content">
  <form onsubmit={handleSubmit} class="RecipeForm">
    <h2>{isEditMode ? 'Edit Recipe' : 'Add New Recipe'}</h2>

    <div>
      <label for="name">Name:</label>
      <input type="text" id="name" bind:value={name} required />
    </div>

    <div>
      <label for="description">Description:</label>
      <textarea id="description" bind:value={description} rows="3"></textarea>
    </div>

    <div>
      <label for="categories">Categories (comma-separated):</label>
      <input type="text" id="categories" bind:value={categories} />
    </div>

    <div>
      <label for="photos">Photos:</label>
      <input type="file" id="photos" accept="image/*" multiple onchange={handlePhotosChange} />
      {#if photoPreviews.length > 0}
        <div class="photo-previews">
          {#each photoPreviews as url, index (url)}
            <span class="preview-wrap">
              <img src={url} alt="" />
              <button type="button" class="thumb-x" aria-label="Remove selected photo" onclick={() => removePhoto(index)}>&times;</button>
            </span>
          {/each}
        </div>
      {/if}
    </div>

    {#if isEditMode && existingImages.length > 0}
      <div>
        <span>Current photos (remove with &times;):</span>
        <div class="photo-previews">
          {#each existingImages as url (url)}
            <span class="preview-wrap">
              <img src={url} alt="" />
              <button type="button" class="thumb-x" aria-label="Remove photo" onclick={() => removeExistingImage(url)}>&times;</button>
            </span>
          {/each}
        </div>
      </div>
    {/if}

    <div>
      <span>Ingredients:</span>
      {#each ingredients as ing, index (index)}
        <div class="ingredient-field">
          <input type="text" placeholder="Quantity" bind:value={ing.quantity} />
          <input type="text" placeholder="Name" bind:value={ing.name} />
          {#if ingredients.length > 1}
            <button type="button" class="danger" onclick={() => removeIngredientField(index)}>Remove</button>
          {/if}
        </div>
      {/each}
      <button type="button" onclick={addIngredientField} class="secondary mb-2">Add Ingredient</button>
    </div>

    <div>
      <label for="instructions">Instructions (one step per line):</label>
      <textarea id="instructions" bind:value={instructions} rows="5"></textarea>
    </div>

    <div style="display: flex; gap: 15px; flex-wrap: wrap">
      <div style="flex-grow: 1; min-width: 120px">
        <label for="prepTime">Prep Time (mins):</label>
        <input type="number" id="prepTime" bind:value={prepTime} />
      </div>
      <div style="flex-grow: 1; min-width: 120px">
        <label for="cookTime">Cook Time (mins):</label>
        <input type="number" id="cookTime" bind:value={cookTime} />
      </div>
      <div style="flex-grow: 1; min-width: 120px">
        <label for="servings">Servings:</label>
        <input type="number" id="servings" bind:value={servings} />
      </div>
    </div>

    <div class="form-actions">
      <button type="submit" disabled={submitting}>{submitting ? 'Saving…' : isEditMode ? 'Save Changes' : 'Add Recipe'}</button>
      <button type="button" onclick={() => goto(isEditMode ? `/recipes/${recipeId}` : '/')}>Cancel</button>
    </div>
  </form>
</div>
