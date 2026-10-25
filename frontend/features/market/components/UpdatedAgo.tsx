"use client";

import { useEffect, useState } from "react";
import { formatAgo } from "@/shared/lib/format";

const STALE_AFTER_SECONDS = 45;

/** "Actualizado hace 4 s". Si las actualizaciones fallan, avisa de que los datos pueden estar viejos. */
export function UpdatedAgo({ updatedAt, failing }: { updatedAt: number; failing: boolean }) {
  const [now, setNow] = useState<number | null>(null);

  useEffect(() => {
    setNow(Date.now());
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  if (now === null) {
    return <span className="text-soft">Actualizado</span>;
  }

  const seconds = Math.max(0, (now - updatedAt) / 1000);
  const stale = failing && seconds > STALE_AFTER_SECONDS;

  return stale ? (
    <span className="text-down" role="status">
      Sin conexión con el servidor. Datos {formatAgo(seconds)}
    </span>
  ) : (
    <span className="text-soft">Actualizado {formatAgo(seconds)}</span>
  );
}
