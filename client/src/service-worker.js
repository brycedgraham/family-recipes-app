/**
 * Minimal PWA service worker (Ticket 11).
 * SvelteKit compiles this file to /service-worker.js in production builds and
 * registers it automatically (kit.serviceWorker.register defaults to true).
 * Android Chrome/Brave require an active registered service worker for installability.
 */

self.addEventListener('install', () => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(self.clients.claim());
});

// Pass-through: always hit the network, no caching — SPA + API must stay fresh.
self.addEventListener('fetch', (event) => {
  event.respondWith(fetch(event.request));
});
