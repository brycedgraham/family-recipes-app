import React, { useState } from 'react';

const STORAGE_KEY = 'theme';

function getInitialTheme() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch { /* storage unavailable */ }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export default function ThemeToggle() {
  const [theme, setTheme] = useState(getInitialTheme);

  const toggle = () => {
    const next = theme === 'dark' ? 'light' : 'dark';
    setTheme(next);
    document.documentElement.setAttribute('data-theme', next);
    try { localStorage.setItem(STORAGE_KEY, next); } catch { /* ignore */ }
  };

  return (
    <button
      type="button"
      className={`theme-toggle${theme === 'dark' ? ' dark' : ''}`}
      role="switch"
      aria-checked={theme === 'dark'}
      aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
      onClick={toggle}
    >
      <span className="theme-toggle-track">
        <span className="theme-toggle-thumb">{theme === 'dark' ? '🌙' : '☀️'}</span>
      </span>
    </button>
  );
}
