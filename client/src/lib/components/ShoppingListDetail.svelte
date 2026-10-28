<script>
  import {
    fetchShoppingList,
    fetchSharedShoppingList,
    addShoppingListItem,
    patchShoppingListItem,
    clearCheckedShoppingItems,
    deleteShoppingListItem,
    fetchStores,
    createStore,
    deleteStore,
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
  let addingStore = $state(false);
  let newStoreName = $state('');

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

  // Assign/unassign a store on an item; also remembers this ingredient's default for future lists.
  async function handleSetStore(item, store) {
    setItem(item.id, { store });
    try {
      // "" clears on the wire (JSON null would mean "not sent").
      list = await patchShoppingListItem(listId, item.id, { store: store ?? '' });
    } catch (err) {
      setItem(item.id, { store: item.store });
      alert(`Update failed: ${err.message}`);
    }
  }

  // Row-level "+ New store…": prompt for a name, register it, then assign it to the item.
  async function handleNewStore(item) {
    const name = window.prompt('New store name:')?.trim();
    if (!name) return;
    busy = true;
    try {
      stores = await createStore(name);
      list = await patchShoppingListItem(listId, item.id, { store: name });
    } catch (err) {
      alert(`Store update failed: ${err.message}`);
      const fresh = await fetchStores().catch(() => null); // resync in case the create itself succeeded
      if (Array.isArray(fresh)) stores = fresh;
    } finally {
      busy = false;
    }
  }

  // Store-bar "+ Store": register a store and jump to its tab.
  async function handleCreateStore(e) {
    e.preventDefault();
    const name = newStoreName.trim();
    if (!name) return;
    busy = true;
    try {
      stores = await createStore(name);
      addingStore = false;
      newStoreName = '';
      storeFilter = name;
    } catch (err) {
      alert(`Create failed: ${err.message}`);
    } finally {
      busy = false;
    }
  }

  async function handleDeleteStore(store) {
    if (!window.confirm(`Remove store "${store}"? Items assigned to it will become unassigned.`)) return;
    busy = true;
    try {
      stores = await deleteStore(store);
      if (storeFilter === store) storeFilter = 'all';
      // Items were unassigned server-side — refetch so the UI reflects that.
      list = sharedToken ? await fetchSharedShoppingList(sharedToken) : await fetchShoppingList(listId);
    } catch (err) {
      alert(`Delete failed: ${err.message}`);
    } finally {
      busy = false;
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
        {#if !readOnly && storeFilter === store}
          <button
            type="button"
            class="sl-store-del"
            title={`Remove store "${store}"? Items assigned to it will become unassigned.`}
            disabled={busy}
            onclick={() => handleDeleteStore(store)}
          >&times;</button>
        {/if}
      {/each}
      <button type="button" class:active={storeFilter === 'unassigned'} onclick={() => (storeFilter = 'unassigned')}>
        Any / Unassigned ({storeCounts.unassigned})
      </button>
      {#if !readOnly}
        {#if addingStore}
          <form class="sl-store-add" onsubmit={handleCreateStore}>
            <input
              type="text"
              placeholder="Store name..."
              maxlength="50"
              bind:value={newStoreName}
              onkeydown={(e) => { if (e.key === 'Escape') { addingStore = false; newStoreName = ''; } }}
            />
            <button type="submit" disabled={busy || !newStoreName.trim()}>Add</button>
          </form>
        {:else}
          <button type="button" class="sl-store-add-toggle" onclick={() => (addingStore = true)}>+ Store</button>
        {/if}
      {/if}
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
              stores={displayStores}
              onSetStore={handleSetStore}
              onNewStore={handleNewStore}
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
                stores={displayStores}
                onSetStore={handleSetStore}
                onNewStore={handleNewStore}
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
