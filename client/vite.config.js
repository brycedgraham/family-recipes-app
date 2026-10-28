import { sveltekit } from '@sveltejs/kit/vite'
import adapter from '@sveltejs/adapter-static'
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [
    sveltekit({
      preprocess: vitePreprocess(),
      // SPA mode: all routes render client-side; the static adapter emits a single
      // index.html fallback that the browser router takes over from.
      adapter: adapter({ fallback: 'index.html', pages: 'dist' }),
    }),
  ],
  server: {
    proxy: {
      // Forward API calls to the .NET backend during development.
      '/api': 'http://localhost:5051',
    },
  },
})
