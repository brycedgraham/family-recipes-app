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

export function createRecipe(payload) {
  return request('/recipes', { method: 'POST', body: JSON.stringify(payload) });
}

export function updateRecipe(id, payload) {
  return request(`/recipes/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
}

export function deleteRecipe(id) {
  return request(`/recipes/${id}`, { method: 'DELETE' });
}
