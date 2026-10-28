<script>
  import { getPhotoImportJobs, uploadRecipePhotos, retryPhotoImportJob, deletePhotoImportJob } from '#lib/api.js';

  let { open, onClose, onRecipesChanged, onActiveChange } = $props();

  const MAX_PHOTOS = 4;
  const MAX_EDGE = 1600; // longest edge after client-side resize (Ticket 15)
  const JPEG_QUALITY = 0.85;

  let photos = $state([]); // resized File objects awaiting upload
  let category = $state('');
  let uploading = $state(false);
  let error = $state(null);
  let status = $state(null); // { endpointOnline, activeModel, endpointUrl, jobs }

  const jobs = $derived(status?.jobs ?? []);
  const activeCount = $derived(jobs.filter(j => j.status === 'Pending' || j.status === 'Processing').length);

  // Job ids already counted as completed; new ones trigger a recipe-list refresh in the parent.
  let completedSeen = new Set();

  // Keep the sidebar badge count in sync even while this modal is closed afterwards.
  $effect(() => onActiveChange?.(activeCount));

  // On open: reset the form, fetch status immediately, poll every 4s (cheap; jobs move through the queue).
  $effect(() => {
    if (!open) return;
    photos = [];
    error = null;
    refresh();
    const timer = setInterval(refresh, 4000);
    function onKey(e) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', onKey);
    return () => {
      clearInterval(timer);
      document.removeEventListener('keydown', onKey);
    };
  });

  async function refresh() {
    try {
      const data = await getPhotoImportJobs();
      status = data;
      const fresh = (data.jobs ?? []).filter(j => j.status === 'Completed' && !completedSeen.has(j.id));
      if (fresh.length > 0) {
        for (const j of fresh) completedSeen.add(j.id);
        onRecipesChanged?.(); // new recipes landed in the catalog
      }
    } catch (err) {
      console.error('Failed to load photo import status:', err);
    }
  }

  // Downscale via canvas so a 12 MP phone shot uploads fast and fits the vision model's context window.
  function resizePhoto(file) {
    return new Promise((resolve, reject) => {
      const url = URL.createObjectURL(file);
      const img = document.createElement('img');
      img.onload = () => {
        URL.revokeObjectURL(url);
        const scale = Math.min(1, MAX_EDGE / Math.max(img.width, img.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(img.width * scale));
        canvas.height = Math.max(1, Math.round(img.height * scale));
        canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
        canvas.toBlob(blob => {
          if (!blob) {
            reject(new Error(`'${file.name}' could not be read.`));
            return;
          }
          const name = file.name.replace(/\.[^.]*$/, '') + '.jpg';
          resolve(new File([blob], name, { type: 'image/jpeg' }));
        }, 'image/jpeg', JPEG_QUALITY);
      };
      img.onerror = () => {
        URL.revokeObjectURL(url);
        reject(new Error(`'${file.name}' could not be read.`));
      };
      img.src = url;
    });
  }

  async function handleFiles(e) {
    const incoming = Array.from(e.target.files ?? []);
    e.target.value = ''; // allow re-picking the same file after removal
    error = null;
    const room = MAX_PHOTOS - photos.length;
    if (incoming.length === 0) return;
    if (room <= 0) {
      error = `At most ${MAX_PHOTOS} photos per import.`;
      return;
    }
    try {
      const resized = await Promise.all(incoming.slice(0, room).map(resizePhoto));
      photos = [...photos, ...resized];
      if (incoming.length > room) error = `Only the first ${room} photo(s) were added (${MAX_PHOTOS} max).`;
    } catch (err) {
      error = err.message || 'Could not read that image.';
    }
  }

  function removePhoto(index) {
    photos = photos.filter((_, i) => i !== index);
  }

  async function handleUpload(e) {
    e.preventDefault();
    if (uploading || photos.length === 0) return;
    uploading = true;
    error = null;
    try {
      await uploadRecipePhotos(photos, category.trim() || undefined);
      photos = [];
      await refresh(); // show the queued job right away
    } catch (err) {
      error = err.message || 'Upload failed.';
    }
    uploading = false;
  }

  async function handleRetry(id) {
    error = null;
    try {
      await retryPhotoImportJob(id);
      await refresh();
    } catch (err) {
      error = err.message || 'Failed to requeue that job.';
    }
  }

  async function handleDelete(id) {
    error = null;
    try {
      await deletePhotoImportJob(id);
      await refresh();
    } catch (err) {
      error = err.message || 'Failed to delete that job.';
    }
  }

  function formatSize(bytes) {
    return bytes > 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  function formatDate(iso) {
    return new Date(iso).toLocaleString();
  }
</script>

<!-- Always mounted so refs stay stable; hidden via CSS when closed (same pattern as ImportUrlModal). -->
<div class="import-overlay" class:hidden={!open} role="dialog" aria-modal="true" aria-labelledby="import-photo-title">
  <div class="import-modal photo-import-modal">
    <h2 id="import-photo-title">Import Recipe from Photo</h2>

    <p class="status-pill" class:online={status?.endpointOnline}>
      <span class="status-dot"></span>
      {#if status === null}
        Checking GPU…
      {:else if status.endpointOnline}
        GPU Online{#if status.activeModel} ({status.activeModel}){/if}
      {:else}
        GPU Offline — photos will queue &amp; import automatically
      {/if}
    </p>

    <p class="import-hint">Snap 1–4 cookbook pages or screenshots. The local vision model reads them and the recipe lands here once it's done.</p>

    <form onsubmit={handleUpload}>
      <div class="picker-row">
        <label class="picker">
          Choose photos
          <input type="file" accept="image/*" multiple onchange={handleFiles} disabled={uploading || photos.length >= MAX_PHOTOS} />
        </label>
        <label class="picker">
          Take photo
          <input type="file" accept="image/*" capture="environment" onchange={handleFiles} disabled={uploading || photos.length >= MAX_PHOTOS} />
        </label>
      </div>

      {#if photos.length > 0}
        <ul class="photo-list">
          {#each photos as photo, i (photo.name + ':' + photo.size + ':' + i)}
            <li>
              <span>{photo.name} · {formatSize(photo.size)}</span>
              <button type="button" class="link-btn" onclick={() => removePhoto(i)} aria-label="Remove {photo.name}">Remove</button>
            </li>
          {/each}
        </ul>
      {/if}

      <input type="text" id="photo-import-category" placeholder="Category (optional)" bind:value={category} disabled={uploading} aria-label="Category for imported recipe" />

      <div class="import-row">
        <button type="submit" disabled={uploading || photos.length === 0}>
          {uploading ? 'Uploading…' : `Start Import${photos.length > 0 ? ` (${photos.length})` : ''}`}
        </button>
      </div>
      {#if error}<p class="import-error" role="alert">{error}</p>{/if}

      <h3>Recent imports</h3>
      {#if jobs.length === 0}
        <p class="import-hint">No photo imports yet.</p>
      {:else}
        <ul class="job-list">
          {#each jobs as job (job.id)}
            <li class="job-row">
              <span class="job-status" class:pending={job.status === 'Pending'} class:processing={job.status === 'Processing'} class:completed={job.status === 'Completed'} class:failed={job.status === 'Failed'}>
                {job.status === 'Processing' ? 'Processing…' : job.status}
              </span>
              <span class="job-main">
                {#if job.status === 'Completed'}
                  <a href="/recipes/{job.recipeId}">{job.recipeTitle || 'View recipe'}</a>
                {:else}
                  {job.recipeTitle ? job.recipeTitle : `${(job.fileNames ?? []).length} photo(s)`} · {formatDate(job.createdAt)}
                {/if}
              </span>
              {#if job.status === 'Failed'}
                <span class="job-actions">
                  <button type="button" class="link-btn" onclick={() => handleRetry(job.id)}>Retry</button>
                  <button type="button" class="link-btn danger" onclick={() => handleDelete(job.id)}>Delete</button>
                </span>
              {/if}
              {#if job.status === 'Completed' || job.status === 'Pending'}
                <span class="job-actions">
                  <button type="button" class="link-btn" onclick={() => handleDelete(job.id)} aria-label="Dismiss job">Dismiss</button>
                </span>
              {/if}
            </li>
            {#if job.status === 'Failed' && job.error}
              <li class="job-error"><span>{job.error}</span></li>
            {/if}
          {/each}
        </ul>
      {/if}

      <div class="import-actions">
        <button type="button" class="secondary" onclick={onClose}>Close</button>
      </div>
    </form>
  </div>
</div>

<style>
  .photo-import-modal {
    max-width: 560px;
  }

  .status-pill {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    padding: 4px 12px;
    border-radius: 999px;
    font-size: 0.85em;
    background-color: var(--hover-bg, rgba(0, 0, 0, 0.06));
    color: #b26a00; /* amber: GPU offline */
  }

  .status-pill.online {
    color: #2e7d32; /* green: GPU online */
  }

  .status-dot {
    width: 9px;
    height: 9px;
    border-radius: 50%;
    background-color: #c07b18;
    flex-shrink: 0;
  }

  .status-pill.online .status-dot {
    background-color: #2e7d32;
  }

  .picker-row {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    margin-bottom: 8px;
  }

  .picker {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    font-size: 0.9em;
  }

  .photo-list,
  .job-list {
    list-style: none;
    margin: 8px 0;
    padding: 0;
  }

  .photo-list li {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    gap: 8px;
    padding: 2px 0;
    font-size: 0.9em;
  }

  .job-row {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 6px 0;
    border-top: 1px solid var(--border-color, rgba(0, 0, 0, 0.12));
  }

  .job-main {
    flex: 1;
    min-width: 0;
    overflow-wrap: anywhere;
    font-size: 0.9em;
  }

  .job-status {
    padding: 1px 9px;
    border-radius: 999px;
    font-size: 0.78em;
    white-space: nowrap;
    background-color: var(--hover-bg, rgba(0, 0, 0, 0.06));
    color: var(--text-secondary, inherit);
  }

  .job-status.pending { color: #b26a00; }
  .job-status.processing { color: #1565c0; }
  .job-status.completed { color: #2e7d32; }
  .job-status.failed { color: #b3261e; }

  .job-error span {
    display: block;
    font-size: 0.82em;
    color: #b3261e;
    padding: 0 0 4px 0;
    overflow-wrap: anywhere;
  }

  .job-actions {
    display: inline-flex;
    gap: 8px;
    flex-shrink: 0;
  }

  .link-btn {
    background: none;
    border: none;
    padding: 0;
    font: inherit;
    font-size: 0.85em;
    color: var(--primary, #2e7d32);
    cursor: pointer;
    text-decoration: underline;
  }

  .link-btn.danger {
    color: #b3261e;
  }

  h3 {
    margin: 16px 0 4px;
    font-size: 1em;
  }
</style>
