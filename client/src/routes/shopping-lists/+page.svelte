<script>
  import { goto } from '$app/navigation';
  import { fetchShoppingLists, createShoppingList, deleteShoppingList } from '#lib/api.js';

  let lists = $state(null); // null while loading
  let error = $state(null);

  fetchShoppingLists()
    .then(data => (lists = data))
    .catch(err => {
      console.error('Failed to load shopping lists:', err);
      error = err.message;
    });

  async function handleCreate() {
    try {
      const list = await createShoppingList({});
      goto(`/shopping-lists/${list.id}`);
    } catch (err) {
      alert(`Create failed: ${err.message}`);
    }
  }

  async function handleDelete(list) {
    if (!window.confirm(`Delete "${list.name}"? This cannot be undone.`)) return;
    try {
      await deleteShoppingList(list.id);
      lists = (lists ?? []).filter(l => l.id !== list.id);
    } catch (err) {
      alert(`Delete failed: ${err.message}`);
    }
  }
</script>

{#if error}
  <div class="App-main-content"><p>Failed to load shopping lists: {error}</p></div>
{:else if lists === null}
  <div class="App-main-content"><p>Loading shopping lists…</p></div>
{:else}
  <div class="App-main-content ShoppingListsPage">
    <nav class="mb-3 RecipePage-nav">
      <button onclick={handleCreate}>✚ New List</button>
    </nav>
    <h1>Shopping Lists</h1>
    {#if lists.length === 0}
      <p>No shopping lists yet. Create one, or add recipes from any recipe page.</p>
    {:else}
      <ul class="sl-index">
        {#each lists as list (list.id)}
          <li class="sl-list-row">
            <a href="/shopping-lists/{list.id}" class="sl-list-link">
              <span class="sl-list-name">{list.name}</span>
              <span class="sl-list-meta">
                {list.itemCount} items · {list.checkedCount} checked · updated {new Date(list.updatedAt).toLocaleDateString()}
              </span>
            </a>
            <button onclick={() => handleDelete(list)} class="danger">Delete</button>
          </li>
        {/each}
      </ul>
    {/if}
  </div>
{/if}
