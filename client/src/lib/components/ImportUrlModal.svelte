<script>
  import { importRecipeFromUrl } from '#lib/api.js';

  let { open, onClose, onImported } = $props();

  let url = $state('');
  let importing = $state(false);
  let error = $state(null);
  let input;

  // Reset the form and focus the field each time the modal opens.
  $effect(() => {
    if (!open) return;
    url = '';
    error = null;
    input?.focus();
    function onKey(e) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  });

  async function handleImport(e) {
    e.preventDefault();
    const target = url.trim();
    if (!target || importing) return;
    importing = true;
    error = null;
    try {
      const recipe = await importRecipeFromUrl(target);
      onImported(recipe);
    } catch (err) {
      error = err.message || 'Failed to import that URL.';
      importing = false;
    }
  }
</script>

<!-- Always mounted so the input ref stays stable; hidden via CSS when closed. -->
<div class="import-overlay" class:hidden={!open} role="dialog" aria-modal="true" aria-labelledby="import-url-title">
  <div class="import-modal">
    <h2 id="import-url-title">Import Recipe from URL</h2>
    <p class="import-hint">Paste the link to any recipe page that embeds schema.org/Recipe data (most food sites do).</p>
    <form onsubmit={handleImport}>
      <div class="import-row">
        <input
          bind:this={input}
          type="url"
          id="import-url-input"
          placeholder="https://example.com/my-recipe"
          bind:value={url}
          disabled={importing}
          aria-label="Recipe page URL"
        />
        <button type="submit" disabled={importing || !url.trim()}>
          {importing ? 'Importing…' : 'Import'}
        </button>
      </div>
      {#if importing}<p class="import-status">Importing recipe…</p>{/if}
      {#if error}<p class="import-error" role="alert">{error}</p>{/if}
      <div class="import-actions">
        <button type="button" class="secondary" onclick={onClose} disabled={importing}>Cancel</button>
      </div>
    </form>
  </div>
</div>
