<script>
  import { page } from '$app/state';
  import ShoppingListDetail from '#lib/components/ShoppingListDetail.svelte';
</script>

<ShoppingListDetail sharedToken={page.params.token} readOnly />
