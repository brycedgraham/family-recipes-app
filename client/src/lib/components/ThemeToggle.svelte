<script>
  const STORAGE_KEY = 'theme';

  function getInitialTheme() {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'light' || stored === 'dark') return stored;
    } catch { /* storage unavailable */ }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  // index.html sets data-theme before first paint; this state mirrors that same logic.
  let theme = $state(getInitialTheme());

  function toggle() {
    const next = theme === 'dark' ? 'light' : 'dark';
    theme = next;
    document.documentElement.setAttribute('data-theme', next);
    try { localStorage.setItem(STORAGE_KEY, next); } catch { /* ignore */ }
  }
</script>

<button
  type="button"
  class="theme-toggle"
  class:dark={theme === 'dark'}
  role="switch"
  aria-checked={theme === 'dark'}
  aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
  onclick={toggle}
>
  <span class="theme-toggle-track">
    <span class="theme-toggle-thumb">{theme === 'dark' ? '🌙' : '☀️'}</span>
  </span>
</button>
