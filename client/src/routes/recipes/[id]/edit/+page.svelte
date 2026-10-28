<script>
  import { page } from '$app/state';
  import RecipeForm from '#lib/components/RecipeForm.svelte';
</script>

<RecipeForm isEditMode recipeId={page.params.id} />
