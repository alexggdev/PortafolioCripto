"use client";

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

/** Alterna claro/oscuro y recuerda la elección en una cookie (el servidor la lee para no parpadear). */
export function ThemeToggle() {
  function toggle() {
    const root = document.documentElement;
    const current =
      root.dataset.theme ?? (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    const next = current === "dark" ? "light" : "dark";

    root.dataset.theme = next;
    document.cookie = `theme=${next}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax`;
  }

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label="Cambiar entre tema claro y oscuro"
      title="Cambiar tema"
      className="inline-flex size-9 items-center justify-center text-ink hover:text-accent"
    >
      {/* Sol: se ve en tema oscuro (para pasar a claro). Luna: se ve en tema claro. */}
      <svg aria-hidden="true" viewBox="0 0 24 24" className="theme-sun size-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
      </svg>
      <svg aria-hidden="true" viewBox="0 0 24 24" className="theme-moon size-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M20.5 14.2A8.5 8.5 0 0 1 9.8 3.5a8.5 8.5 0 1 0 10.7 10.7z" />
      </svg>
    </button>
  );
}
