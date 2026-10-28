<script>
  import { page } from '$app/state';
  import { goto } from '$app/navigation';
  import {
    fetchMealPlanWeek,
    addMealPlanEntry,
    patchMealPlanEntry,
    deleteMealPlanEntry,
    buildMealPlanShoppingList,
    fetchRecipes,
  } from '#lib/api.js';
  import { mondayOf, addDays, toISO, todayISO, fmtLong } from '#lib/dates.js';

  const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const MEAL_TYPES = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

  // ?week= accepts any date in the week; the API normalizes to Monday.
  const weekParam = $derived(page.url.searchParams.get('week') || todayISO());
  const monday = $derived(mondayOf(weekParam));
  const weekLabel = $derived(fmtLong(monday));
  const todayStr = $derived(todayISO());

  let plan = $state(null); // null while loading
  let loadError = $state(null);
  let recipes = $state([]);
  let saving = $state(false);
  let building = $state(false);
  let buildMsg = $state(null);

  // Modal: null = closed; otherwise the entry being edited (has id) or a new-entry seed.
  let modal = $state(null);
  let form = $state({ dayOfWeek: 0, mealType: 'Breakfast', recipeId: '', customTitle: '', multiplier: 1, notes: '' });
  let formError = $state(null);

  function entriesFor(day) {
    return plan ? plan.entries.filter((e) => e.dayOfWeek === day) : [];
  }

  function openAdd(dayOfWeek) {
    buildMsg = null;
    modal = {};
    form = { dayOfWeek, mealType: 'Breakfast', recipeId: '', customTitle: '', multiplier: 1, notes: '' };
    formError = null;
  }

  function openEdit(entry) {
    buildMsg = null;
    modal = entry;
    form = {
      dayOfWeek: entry.dayOfWeek,
      mealType: entry.mealType,
      recipeId: entry.recipeId ?? '',
      customTitle: entry.customTitle ?? '',
      multiplier: entry.multiplier,
      notes: entry.notes ?? '',
    };
    formError = null;
  }

  function closeModal() {
    if (saving) return;
    modal = null;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (!form.recipeId && !form.customTitle.trim()) {
      formError = 'Pick a recipe or enter a custom title.';
      return;
    }
    const payload = {
      dayOfWeek: Number(form.dayOfWeek),
      mealType: form.mealType,
      recipeId: form.recipeId || null,
      customTitle: form.customTitle.trim() || null,
      multiplier: Number(form.multiplier) || 1,
      notes: form.notes.trim() || undefined,
    };
    saving = true;
    formError = null;
    try {
      if (modal.id) await patchMealPlanEntry(modal.id, payload);
      else await addMealPlanEntry(weekParam, payload);
      modal = null;
      await load();
    } catch (err) {
      formError = err.message;
    } finally {
      saving = false;
    }
  }

  async function handleDelete(entry) {
    const title = entry.recipeTitle || entry.customTitle || 'this meal';
    if (!confirm(`Remove ${title} from the plan?`)) return;
    try {
      await deleteMealPlanEntry(entry.id);
      await load();
    } catch (err) {
      alert(`Delete failed: ${err.message}`);
    }
  }

  async function handleBuildList() {
    building = true;
    buildMsg = null;
    try {
      const list = await buildMealPlanShoppingList(weekParam);
      await goto(`/shopping-lists/${list.id}`);
    } catch (err) {
      buildMsg = `Build failed: ${err.message}`;
    } finally {
      building = false;
    }
  }

  async function load() {
    loadError = null;
    plan = null;
    try {
      plan = await fetchMealPlanWeek(weekParam);
      if (recipes.length === 0) recipes = await fetchRecipes();
    } catch (err) {
      loadError = err.message;
    }
  }

  $effect(() => {
    load(); // runs on mount and whenever ?week= changes
  });

  // Escape closes the modal (document-level so it works regardless of focus).
  $effect(() => {
    if (!modal) return;
    function onKey(e) {
      if (e.key === 'Escape') closeModal();
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  });
</script>

<div class="MealPlan">
  <div class="mp-header">
    <div class="mp-week-nav">
      <button class="secondary mp-nav-btn" onclick={() => goto(`/meal-plan?week=${toISO(addDays(monday, -7))}`)} aria-label="Previous week">‹</button>
      <h1 class="mp-week-label">Week of {weekLabel}</h1>
      <button class="secondary mp-nav-btn" onclick={() => goto(`/meal-plan?week=${toISO(addDays(monday, 7))}`)} aria-label="Next week">›</button>
    </div>
    <button onclick={handleBuildList} disabled={building || !plan || plan.entries.length === 0}>
      {building ? 'Building…' : 'Build Week Shopping List'}
    </button>
  </div>

  {#if buildMsg}<p class="mp-error">{buildMsg}</p>{/if}
  {#if loadError}<p class="mp-error">Failed to load meal plan: {loadError}</p>{/if}
  {#if !plan && !loadError}<p>Loading…</p>{/if}

  {#if plan}
    <div class="mp-grid">
      {#each DAYS as dayName, i (dayName)}
        <div class="mp-day" class:today={toISO(addDays(monday, i)) === todayStr}>
          <h3>{dayName}</h3>
          <div class="mp-entries">
            {#each entriesFor(i) as entry (entry.id)}
              <div class="mp-entry">
                <span class="mp-mealtype">{entry.mealType}</span>
                {#if entry.recipeId}
                  <a href="/recipes/{entry.recipeId}" class="mp-title">{entry.recipeTitle}</a>
                {:else}
                  <span class="mp-title">{entry.customTitle}</span>
                {/if}
                {#if entry.multiplier !== 1}<span class="mp-mult">{entry.multiplier}x</span>{/if}
                {#if entry.notes}<p class="mp-notes">{entry.notes}</p>{/if}
                <span class="mp-entry-actions">
                  <button class="mp-icon" title="Edit" onclick={() => openEdit(entry)}>✎</button>
                  <button class="mp-icon mp-icon-danger" title="Remove" onclick={() => handleDelete(entry)}>✕</button>
                </span>
              </div>
            {:else}
              <p class="mp-empty">—</p>
            {/each}
          </div>
          <button class="mp-add" onclick={() => openAdd(i)}>+ Add</button>
        </div>
      {/each}
    </div>
  {/if}

  {#if modal}
    <div class="mp-overlay" role="button" tabindex="-1"
      onclick={(e) => e.target === e.currentTarget && closeModal()}
      onkeydown={(e) => e.key === 'Escape' && closeModal()}>
      <form class="mp-modal" onsubmit={handleSubmit}>
        <h2>{modal.id ? 'Edit Meal' : 'Add Meal'}</h2>

        <div class="mp-row">
          <label class="mp-field">
            Day
            <select bind:value={form.dayOfWeek}>
              {#each DAYS as dayName, i (dayName)}
                <option value={i}>{dayName}</option>
              {/each}
            </select>
          </label>
          <label class="mp-field">
            Meal
            <select bind:value={form.mealType}>
              {#each MEAL_TYPES as t (t)}
                <option value={t}>{t}</option>
              {/each}
            </select>
          </label>
        </div>

        <label class="mp-field">
          Recipe
          <select bind:value={form.recipeId}>
            <option value="">— custom meal —</option>
            {#each recipes as r (r.id)}
              <option value={r.id}>{r.name}</option>
            {/each}
          </select>
        </label>

        {#if !form.recipeId}
          <label class="mp-field">
            Custom title
            <input type="text" placeholder="e.g. Leftovers, Grocery run…" bind:value={form.customTitle} />
          </label>
        {/if}

        <div class="mp-row">
          <label class="mp-field">
            Multiplier
            <input type="number" min="0.5" step="0.5" bind:value={form.multiplier} />
          </label>
          <label class="mp-field mp-grow">
            Notes (optional)
            <input type="text" placeholder="e.g. double the sauce" bind:value={form.notes} />
          </label>
        </div>

        {#if formError}<p class="mp-error">{formError}</p>{/if}

        <div class="mp-actions">
          <button type="button" class="secondary" onclick={closeModal}>Cancel</button>
          <button type="submit" disabled={saving}>{saving ? 'Saving…' : modal.id ? 'Save Changes' : 'Add Meal'}</button>
        </div>
      </form>
    </div>
  {/if}
</div>
