// Thin fetch layer for the .NET API (proxied to /api by Vite in dev).

const BASE = '/api';

async function request(path, options = {}) {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    let message = `Request failed with status ${res.status}`;
    try {
      const body = await res.json();
      if (typeof body === 'string' && body) message = body;
      else if (body && typeof body.title === 'string') message = body.title;
    } catch {
      // non-JSON error body; keep the status message
    }
    throw new Error(message);
  }
  return res.status === 204 ? null : res.json();
}

export function fetchRecipes({ q, category } = {}) {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  if (category) params.set('category', category);
  const qs = params.toString();
  return request(qs ? `/recipes?${qs}` : '/recipes');
}

export function fetchRecipe(id) {
  return request(`/recipes/${id}`);
}

export function fetchCategories() {
  return request('/categories');
}

// Store names shopping lines can be routed to (defaults first, then custom stores in use).
export function fetchStores() {
  return request('/stores');
}

// Register a new store; returns the updated list of known stores.
export function createStore(name) {
  return request('/stores', { method: 'POST', body: JSON.stringify({ name }) });
}

// Remove a registered store (items/ingredients assigned to it become unassigned); returns the updated list.
export function deleteStore(name) {
  return request(`/stores/${encodeURIComponent(name)}`, { method: 'DELETE' });
}

export function createRecipe(payload) {
  return request('/recipes', { method: 'POST', body: JSON.stringify(payload) });
}

export function updateRecipe(id, payload) {
  return request(`/recipes/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
}

export function deleteRecipe(id) {
  return request(`/recipes/${id}`, { method: 'DELETE' });
}

// Import a recipe from the schema.org/Recipe JSON-LD embedded in an arbitrary web page.
export function importRecipeFromUrl(url) {
  return request('/recipes/import-url', { method: 'POST', body: JSON.stringify({ url }) });
}

// Photo recipe imports (Ticket 15): photos are staged server-side and extracted asynchronously by the
// vision worker; `files` is a File[]/FileList-carrying array of 1-4 images, category optional.
export function uploadRecipePhotos(files, category) {
  const form = new FormData();
  for (const file of files) form.append('files', file);
  if (category) form.append('category', category);
  return request('/recipes/import-photo', { method: 'POST', headers: {}, body: form });
}

// Recent import jobs + live vision-endpoint status: { endpointOnline, activeModel, endpointUrl, jobs }.
export function getPhotoImportJobs() {
  return request('/recipes/import-photo/jobs');
}

export function retryPhotoImportJob(id) {
  return request(`/recipes/import-photo/jobs/${id}/retry`, { method: 'POST' });
}

export function deletePhotoImportJob(id) {
  return request(`/recipes/import-photo/jobs/${id}`, { method: 'DELETE' });
}

// Recipe images (multipart; no JSON content-type so fetch sets the form boundary)
export async function uploadRecipeImage(recipeId, file) {
  const form = new FormData();
  form.append('files', file);
  const res = await fetch(`${BASE}/recipes/${recipeId}/images`, { method: 'POST', body: form });
  if (!res.ok) {
    let message = `Image upload failed with status ${res.status}`;
    try {
      const body = await res.json();
      if (typeof body === 'string' && body) message = body;
      else if (body && typeof body.title === 'string') message = body.title;
    } catch {
      // non-JSON error body; keep the status message
    }
    throw new Error(message);
  }
  return res.json();
}

export function deleteRecipeImage(recipeId, fileName) {
  return request(`/recipes/${recipeId}/images/${encodeURIComponent(fileName)}`, { method: 'DELETE' });
}

// Make a photo the recipe's cover (moves it to the front of the gallery); returns the updated recipe.
export function setRecipeCoverImage(recipeId, fileName) {
  return request(`/recipes/${recipeId}/images/${encodeURIComponent(fileName)}/cover`, { method: 'PUT' });
}

// Shopping lists
export function fetchShoppingLists() {
  return request('/shopping-lists');
}

export function fetchShoppingList(id) {
  return request(`/shopping-lists/${id}`);
}

export function fetchSharedShoppingList(token) {
  return request(`/shopping-lists/shared/${token}`);
}

export function createShoppingList(payload = {}) {
  return request('/shopping-lists', { method: 'POST', body: JSON.stringify(payload) });
}

export function addRecipesToShoppingList(id, items) {
  return request(`/shopping-lists/${id}/add-recipes`, { method: 'POST', body: JSON.stringify({ items }) });
}

export function addShoppingListItem(id, rawText, store = null) {
  return request(`/shopping-lists/${id}/items`, { method: 'POST', body: JSON.stringify({ rawText, store }) });
}

export function patchShoppingListItem(id, itemId, payload) {
  return request(`/shopping-lists/${id}/items/${itemId}`, { method: 'PATCH', body: JSON.stringify(payload) });
}

export function clearCheckedShoppingItems(id) {
  return request(`/shopping-lists/${id}/items/checked`, { method: 'DELETE' });
}

export function deleteShoppingListItem(id, itemId) {
  return request(`/shopping-lists/${id}/items/${itemId}`, { method: 'DELETE' });
}

export function deleteShoppingList(id) {
  return request(`/shopping-lists/${id}`, { method: 'DELETE' });
}

// Meal plans
export function fetchMealPlanWeek(date) {
  return request(`/meal-plans/week/${date}`);
}

export function addMealPlanEntry(date, payload) {
  return request(`/meal-plans/week/${date}/entries`, { method: 'POST', body: JSON.stringify(payload) });
}

export function patchMealPlanEntry(entryId, payload) {
  return request(`/meal-plans/entries/${entryId}`, { method: 'PATCH', body: JSON.stringify(payload) });
}

export function deleteMealPlanEntry(entryId) {
  return request(`/meal-plans/entries/${entryId}`, { method: 'DELETE' });
}

export function buildMealPlanShoppingList(date, payload = {}) {
  return request(`/meal-plans/week/${date}/shopping-list`, { method: 'POST', body: JSON.stringify(payload) });
}
