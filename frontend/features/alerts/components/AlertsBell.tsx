"use client";

import Link from "next/link";
import { NAV_LINK } from "@/shared/lib/ui";
import { useEffect, useState } from "react";
import { ALERTS_CHANGED_EVENT } from "@/features/alerts/lib/alerts";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 30_000;

/** Enlace a las alertas con el número de avisos sin ver. Solo se muestra con sesión iniciada. */
export function AlertsBell() {
  const [tick, setTick] = useState(0);
  // El parámetro t no lo usa la API: cambia la URL para pedir el número al instante cuando la página de alertas avisa.
  const { data } = usePolledJson<{ count: number }>(`/api/alerts/unread-count?t=${tick}`, { count: 0 }, POLL_MS, {
    fetchOnMount: true,
  });

  useEffect(() => {
    const onChanged = () => setTick((n) => n + 1);
    window.addEventListener(ALERTS_CHANGED_EVENT, onChanged);
    return () => window.removeEventListener(ALERTS_CHANGED_EVENT, onChanged);
  }, []);

  const count = data.count;
  const label = count > 0 ? `Alertas, ${count} ${count === 1 ? "aviso sin ver" : "avisos sin ver"}` : "Alertas";

  return (
    <Link href="/alertas" aria-label={label} title={label} className={`relative ${NAV_LINK}`}>
      <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6 9a6 6 0 1 1 12 0c0 6 2.5 7.5 2.5 7.5h-17S6 15 6 9Z" />
        <path d="M10 20a2 2 0 0 0 4 0" />
      </svg>
      <span className="hidden sm:inline">Alertas</span>
      {count > 0 && (
        <span className="ml-0.5 min-w-5 bg-accent px-1.5 text-center text-xs font-bold text-on-accent tabular-nums" aria-hidden="true">
          {count > 99 ? "99+" : count}
        </span>
      )}
    </Link>
  );
}
