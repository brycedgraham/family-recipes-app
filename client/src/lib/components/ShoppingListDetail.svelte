<script>
  import {
    fetchShoppingList,
    fetchSharedShoppingList,
    addShoppingListItem,
    patchShoppingListItem,
    clearCheckedShoppingItems,
    deleteShoppingListItem,
  } from '#lib/api.js';
  import { formatQuantity } from '#lib/format.js';

  let { listId = null, sharedToken = null, readOnly = false } = $props();

  let list = $state(null);
  let notFound = $state(false);
  let rawText = $state('');
  let busy = $state(false);
  let copied = $state(false);

  $effect(() => {
    let cancelled = false;
    const loader = sharedToken ? fetchSharedShoppingList(sharedToken) : fetchShoppingList(listId);
    loader
      .then(data => { if (!cancelled) list = data; })
      .catch(err => {
        console.error('Failed to load shopping list:', err);
        if (!cancelled) notFound = true;
      });
    return () => { cancelled = true; };
  });

  // Server already orders items by category, then checked, then name — group preserving that order.
  let groups = $derived.by(() => {
    if (!list) return [];
    const m = new Map();
    for (const item of list.items) {
      const key = item.category || 'Other';
      if (!m.has(key)) m.set(key, []);
      m.get(key).push(item);
    }
    return [...m.entries()];
  });

  let checkedCount = $derived(list ? list.items.filter(i => i.isChecked).length : 0);
  let shareUrl = $derived(list ? `${window.location.origin}/shopping-lists/shared/${list.shareToken}` : '');

  async function handleQuickAdd(e) {
    e.preventDefault();
    if (!rawText.trim()) return;
    busy = true;
    try {
      list = await addShoppingListItem(listId, rawText);
      rawText = '';
    } catch (err) {
      alert(`Add failed: ${err.message}`);
    } finally {
      busy = false;
    }
  }

  // Optimistic toggle: flip locally, sync with the server, roll back on failure.
  async function handleToggle(item) {
    const next = !item.isChecked;
    list = { ...list, items: list.items.map(i => (i.id === item.id ? { ...i, isChecked: next } : i)) };
    try {
      list = await patchShoppingListItem(listId, item.id, { isChecked: next });
    } catch (err) {
      list = { ...list, items: list.items.map(i => (i.id === item.id ? { ...i, isChecked: !next } : i)) };
      alert(`Update failed: ${err.message}`);
    }
  }

  async function handleClearChecked() {
    if (!window.confirm('Remove all checked items?')) return;
    busy = true;
    try {
      list = await clearCheckedShoppingItems(listId);
    } catch (err) {
      alert(`Clear failed: ${err.message}`);
    } finally {
      busy = false;
    }
  }

  async function handleDeleteItem(item) {
    busy = true;
    try {
      list = await deleteShoppingListItem(listId, item.id);
    } catch (err) {
      alert(`Delete failed: ${err.message}`);
    } finally {
      busy = false;
    }
  }

  async function handleShare() {
    try {
      await navigator.clipboard.writeText(shareUrl);
      copied = true;
      setTimeout(() => (copied = false), 2000);
    } catch {
      window.prompt('Copy this share link:', shareUrl);
    }
  }

  function recipeSourceBadges(item) {
    return (item.recipeSources ?? '').split(',').map(s => s.trim()).filter(Boolean);
  }
</script>

{#if notFound}
  <div class="App-main-content">
    <p>Shopping list not found.</p>
    <a href="/shopping-lists" class="button-link">Back to lists</a>
  </div>
{:else if !list}
  <div class="App-main-content"><p>Loading shopping list…</p></div>
{:else}
  <div class="App-main-content ShoppingListDetail">
    <nav class="mb-3 RecipePage-nav">
      <a href="/shopping-lists" class="secondary">&larr; Lists</a>
      {#if !readOnly && checkedCount > 0}
        <button onclick={handleClearChecked} disabled={busy} class="danger ml-2">
          Clear Checked ({checkedCount})
        </button>
      {/if}
      <button onclick={handleShare} class="ml-2">{copied ? 'Copied!' : 'Share List'}</button>
    </nav>

    <h1>{list.name}</h1>

    {#if !readOnly}
      <form class="sl-quickadd" onsubmit={handleQuickAdd}>
        <input type="text" placeholder='Quick add, e.g. "2 gallons whole milk"' bind:value={rawText} />
        <button type="submit" disabled={busy || !rawText.trim()}>Add</button>
      </form>
    {/if}

    {#each groups as [category, items] (category)}
      <section class="sl-group">
        <h2>{category} <span class="sl-count">{items.length}</span></h2>
        <ul>
          {#each items as item (item.id)}
            <li class="sl-item" class:checked={item.isChecked}>
              <input
                type="checkbox"
                checked={item.isChecked}
                disabled={readOnly || busy}
                onchange={() => handleToggle(item)}
              />
              <span class="sl-qty">{formatQuantity(item.quantity, item.unit)}</span>
              <span class="sl-name">{item.name}</span>
              {#if item.notes}<span class="sl-notes">{item.notes}</span>{/if}
              {#each recipeSourceBadges(item) as src (src)}
                <span class="sl-badge">from {src}</span>
              {/each}
              {#if !readOnly}
                <button class="sl-del" title="Remove item" disabled={busy} onclick={() => handleDeleteItem(item)}>&times;</button>
              {/if}
            </li>
          {/each}
        </ul>
      </section>
    {/each}
  </div>
{/if}
