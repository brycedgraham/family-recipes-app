<script>
  import '../app.css';
  import { page } from '$app/state';
  import ThemeToggle from '#lib/components/ThemeToggle.svelte';

  let { children } = $props();

  const recipesActive = () => page.url.pathname === '/' || page.url.pathname.startsWith('/recipes');
  const planActive = () => page.url.pathname.startsWith('/meal-plan');
  const listsActive = () => page.url.pathname.startsWith('/shopping-lists');
</script>

<nav class="App-topnav">
  <a href="/" class:active={recipesActive()}>Recipes</a>
  <a href="/meal-plan" class:active={planActive()}>Meal Plan</a>
  <a href="/shopping-lists" class:active={listsActive()}>Shopping Lists</a>
  <span style="margin-left: auto"></span>
  <ThemeToggle />
</nav>

{@render children()}
