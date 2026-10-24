<script>
  import { formatQuantity } from '#lib/format.js';

  let {
    item,
    readOnly = false,
    busy = false,
    onToggle,
    onDelete,
    onCycleStore,
    onTogglePantry,
  } = $props();

  function recipeSourceBadges(it) {
    return (it.recipeSources ?? '').split(',').map(s => s.trim()).filter(Boolean);
  }
</script>

<li class="sl-item" class:checked={item.isChecked}>
  <input
    type="checkbox"
    checked={item.isChecked}
    disabled={readOnly || busy}
    onchange={() => onToggle(item)}
  />
  <span class="sl-qty">{formatQuantity(item.quantity, item.unit)}</span>
  <span class="sl-name">{item.name}</span>
  {#if !readOnly}
    <button
      type="button"
      class="sl-store-pill"
      class:assigned={!!item.store}
      title="Set store (also remembers this ingredient's default for future lists)"
      disabled={busy}
      onclick={() => onCycleStore(item)}
    >
      {item.store ?? '+ Store'}
    </button>
    <button
      type="button"
      class="sl-pantry-btn"
      class:on={item.isPantryStaple}
      title={item.isPantryStaple ? 'Remove from pantry staples' : 'Mark as pantry staple (already have?)'}
      disabled={busy}
      onclick={() => onTogglePantry(item)}
    >
      {item.isPantryStaple ? '✓ pantry' : '+ pantry'}
    </button>
  {/if}
  {#if item.notes}<span class="sl-notes">{item.notes}</span>{/if}
  {#each recipeSourceBadges(item) as src (src)}
    <span class="sl-badge">from {src}</span>
  {/each}
  {#if !readOnly}
    <button class="sl-del" title="Remove item" disabled={busy} onclick={() => onDelete(item)}>&times;</button>
  {/if}
</li>
