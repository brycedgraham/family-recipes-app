<script>
  import {
    fetchShoppingList,
    fetchSharedShoppingList,
    addShoppingListItem,
    patchShoppingListItem,
    clearCheckedShoppingItems,
    deleteShoppingListItem,
    fetchStores,
  } from '#lib/api.js';
  import ShoppingListItemRow from './ShoppingListItemRow.svelte';

  let { listId = null, sharedToken = null, readOnly = false } = $props();

  let list = $state(null);
  let notFound = $state(false);
  let rawText = $state('');
  let busy = $state(false);
  let copied = $state(false);
  let stores = $state(['WinCo', 'Costco']);
  // 'all' | 'unassigned' | a store name
  let storeFilter = $state('all');
  let pantryOpen = $state(true);

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

  $effect(() => {
    let cancelled = false;
    fetchStores()
      .then(data => { if (!cancelled && Array.isArray(data)) stores = data; })
      .catch(() => {}); // API without /stores: keep the default pair
    return () => { cancelled = true; };
  });

  // Tabs also show any store already present on items but missing from the fetched list.
  let displayStores = $derived.by(() => {
    const itemStores = new Set((list?.items ?? []).map(i => i.store).filter(Boolean));
    const extras = [...itemStores]
      .filter(s => !stores.some(x => x.toLowerCase() === s.toLowerCase()))
      .sort((a, b) => a.localeCompare(b));
    return [...stores, ...extras];
  });

  function matchesStore(item) {
    if (storeFilter === 'all') return true;
    if (storeFilter === 'unassigned') return !item.store;
    return item.store === storeFilter;
  }

  // Unchecked pantry staples are pulled out of the aisle groups into the Pantry Check section.
  let pantryItems = $derived(list ? list.items.filter(i => i.isPantryStaple && !i.isChecked && matchesStore(i)) : []);
  let mainItems = $derived(list ? list.items.filter(i => !(i.isPantryStaple && !i.isChecked) && matchesStore(i)) : []);

  // Server already orders items by category, then checked, then name — group preserving that order.
  let groups = $derived.by(() => {
    const m = new Map();
    for (const item of mainItems) {
      const key = item.category || 'Other';
      if (!m.has(key)) m.set(key, []);
      m.get(key).push(item);
    }
    return [...m.entries()];
  });

  let storeCounts = $derived.by(() => {
    const counts = Object.fromEntries(displayStores.map(s => [s, 0]));
    let unassigned = 0;
    for (const item of list?.items ?? []) {
      if (!item.store) unassigned++;
      else counts[item.store] = (counts[item.store] ?? 0) + 1;
    }
    return { ...counts, unassigned };
  });

  let checkedCount = $derived(list ? list.items.filter(i => i.isChecked).length : 0);
  let shareUrl = $derived(list ? `${window.location.origin}/shopping-lists/shared/${list.shareToken}` : '');

  function setItem(id, patch) {
    list = { ...list, items: list.items.map(i => (i.id === id ? { ...i, ...patch } : i)) };
  }

  async function handleQuickAdd(e) {
    e.preventDefault();
    if (!rawText.trim()) return;
    busy = true;
    try {
      // When a specific store tab is active, newly quick-added items default to that store.
      const store = storeFilter !== 'all' && storeFilter !== 'unassigned' ? storeFilter : null;
      list = await addShoppingListItem(listId, rawText, store);
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
    setItem(item.id, { isChecked: next });
    try {
      list = await patchShoppingListItem(listId, item.id, { isChecked: next });
    } catch (err) {
      setItem(item.id, { isChecked: !next });
      alert(`Update failed: ${err.message}`);
    }
  }

  // One tap cycles unassigned → each known store → back to unassigned.
  function nextStore(current) {
    const cycle = [null, ...displayStores];
    const idx = current ? cycle.indexOf(current) : 0;
    return cycle[(idx + 1) % cycle.length];
  }

  async function handleCycleStore(item) {
    const next = nextStore(item.store);
    setItem(item.id, { store: next });
    try {
      // "" clears on the wire (JSON null would mean "not sent"). The server also remembers this
      // ingredient's default store for future weekly lists.
      list = await patchShoppingListItem(listId, item.id, { store: next ?? '' });
    } catch (err) {
      setItem(item.id, { store: item.store });
      alert(`Update failed: ${err.message}`);
    }
  }

  async function handleTogglePantry(item) {
    const next = !item.isPantryStaple;
    setItem(item.id, { isPantryStaple: next });
    try {
      list = await patchShoppingListItem(listId, item.id, { isPantryStaple: next });
    } catch (err) {
      setItem(item.id, { isPantryStaple: !next });
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

    <div class="sl-storebar">
      <button type="button" class:active={storeFilter === 'all'} onclick={() => (storeFilter = 'all')}>
        All ({list.items.length})
      </button>
      {#each displayStores as store (store)}
        <button type="button" class:active={storeFilter === store} onclick={() => (storeFilter = store)}>
          {store} ({storeCounts[store] ?? 0})
        </button>
      {/each}
      <button type="button" class:active={storeFilter === 'unassigned'} onclick={() => (storeFilter = 'unassigned')}>
        Any / Unassigned ({storeCounts.unassigned})
      </button>
    </div>

    {#each groups as [category, items] (category)}
      <section class="sl-group">
        <h2>{category} <span class="sl-count">{items.length}</span></h2>
        <ul>
          {#each items as item (item.id)}
            <ShoppingListItemRow
              item={item}
              readOnly={readOnly}
              busy={busy}
              onToggle={handleToggle}
              onDelete={handleDeleteItem}
              onCycleStore={handleCycleStore}
              onTogglePantry={handleTogglePantry}
            />
          {/each}
        </ul>
      </section>
    {/each}

    {#if pantryItems.length > 0}
      <section class="sl-group sl-pantry">
        <h2>
          <button type="button" class="sl-pantry-toggle" onclick={() => (pantryOpen = !pantryOpen)}>
            {pantryOpen ? '▾' : '▸'} Pantry Check — Already Have? <span class="sl-count">{pantryItems.length}</span>
          </button>
        </h2>
        {#if pantryOpen}
          <ul>
            {#each pantryItems as item (item.id)}
              <ShoppingListItemRow
                item={item}
                readOnly={readOnly}
                busy={busy}
                onToggle={handleToggle}
                onDelete={handleDeleteItem}
                onCycleStore={handleCycleStore}
                onTogglePantry={handleTogglePantry}
              />
            {/each}
          </ul>
        {/if}
      </section>
    {/if}

    {#if mainItems.length === 0 && pantryItems.length === 0}
      <p class="sl-empty">
        No {storeFilter === 'all' ? '' : storeFilter === 'unassigned' ? 'unassigned' : storeFilter + ' '}items on this list.
      </p>
    {/if}
  </div>
{/if}
