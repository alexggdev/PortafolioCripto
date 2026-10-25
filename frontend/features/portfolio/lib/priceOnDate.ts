import type { ChartPoint } from "../../../shared/lib/types";

// Precio de cierre de una moneda en un día, a partir de su histórico diario. Sin React, para poder probarlo.

export type PriceLookup =
  | { status: "found"; priceUsd: number; date: string }
  /** La fecha es anterior al primer dato que tenemos de la moneda. */
  | { status: "before"; firstDate: string }
  | { status: "none" };

/**
 * Precio de cierre del día UTC de `iso`. Si ese día no tiene dato, se usa el último anterior; si el día es posterior
 * al último dato (hoy), el último disponible. `points` debe venir ordenado por fecha.
 */
export function priceOnDate(points: readonly ChartPoint[], iso: string): PriceLookup {
  const day = iso.slice(0, 10);
  const valid = points.filter((p) => p.priceUsd > 0);
  const first = valid[0];
  if (!first || !/^\d{4}-\d{2}-\d{2}$/.test(day)) return { status: "none" };
  if (day < first.date) return { status: "before", firstDate: first.date };

  let found = first;
  for (const p of valid) {
    if (p.date > day) break;
    found = p;
  }
  return { status: "found", priceUsd: found.priceUsd, date: found.date };
}
