"use client";

import { useState } from "react";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { setWatched } from "@/features/watchlist/lib/watchlist";

/**
 * Estrella para seguir o dejar de seguir una moneda.
 * Sin sesión lleva al login (y vuelve a la misma página). Con sesión cambia al instante y deshace el cambio si la API falla.
 */
export function WatchStar({
  slug,
  name,
  initialWatched,
  signedIn,
  onChange,
}: {
  slug: string;
  name: string;
  initialWatched: boolean;
  signedIn: boolean;
  /** Aviso al padre cuando el cambio se ha guardado (la lista de seguimiento quita la fila). */
  onChange?: (watched: boolean) => void;
}) {
  const [watched, setWatchedState] = useState(initialWatched);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function toggle() {
    if (!signedIn) {
      window.location.assign(`/login?next=${encodeURIComponent(window.location.pathname + window.location.search)}`);
      return;
    }
    if (busy) return;

    const next = !watched;
    setBusy(true);
    setError(null);
    setWatchedState(next);

    const result = await setWatched(slug, next);
    setBusy(false);

    if (result.ok) {
      onChange?.(next);
      return;
    }
    setWatchedState(!next);
    if (result.status === 401) {
      redirectToLogin();
      return;
    }
    setError(result.errors.join(" "));
  }

  const label = watched ? `Dejar de seguir ${name}` : `Seguir ${name}`;

  return (
    <span className="inline-flex items-center">
      <button
        type="button"
        onClick={toggle}
        aria-pressed={watched}
        aria-label={label}
        title={signedIn ? label : `Entra para seguir ${name}`}
        className={`inline-flex size-8 items-center justify-center ${watched ? "text-accent" : "text-soft hover:text-ink"}`}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" strokeWidth="1.8" stroke="currentColor" fill={watched ? "currentColor" : "none"} strokeLinejoin="round">
          <path d="m12 3 2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z" />
        </svg>
      </button>
      <span role="status" className={error ? "ml-1 text-xs text-down" : "sr-only"}>
        {error}
      </span>
    </span>
  );
}
