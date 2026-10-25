import type { PositionDto } from "../../../shared/lib/types";

// Orden de la tabla de activos del portafolio. Sin React, para poder probarlo.

export type PositionSortKey = "name" | "price" | "change1h" | "change24h" | "change7d" | "value" | "quantity" | "avgCost" | "pnl";
export type SortDir = "asc" | "desc";

function valueOf(p: PositionDto, key: PositionSortKey): number | string | null {
  switch (key) {
    case "name":
      return p.name.toLowerCase();
    case "price":
      return p.priceUsd;
    case "change1h":
      return p.change1hPct ?? null;
    case "change24h":
      return p.change24hPct;
    case "change7d":
      return p.change7dPct ?? null;
    case "value":
      return p.valueUsd;
    case "quantity":
      return p.quantity;
    case "avgCost":
      return p.averageCostUsd;
    case "pnl":
      return p.unrealizedPnlUsd;
  }
}

/** Copia ordenada. Lo que no tiene valor (sin precio, sin dato de 1 h…) va siempre al final, ordenes como ordenes. */
export function sortPositions(positions: readonly PositionDto[], key: PositionSortKey, dir: SortDir): PositionDto[] {
  const sign = dir === "asc" ? 1 : -1;
  return [...positions].sort((a, b) => {
    const x = valueOf(a, key);
    const y = valueOf(b, key);
    if (x === null && y === null) return a.symbol.localeCompare(b.symbol);
    if (x === null) return 1;
    if (y === null) return -1;
    if (typeof x === "string" && typeof y === "string") return sign * x.localeCompare(y, "es");
    return sign * ((x as number) - (y as number));
  });
}

/**
 * Como `nextSort`, pero si la primera pulsación de otra columna dejaría las filas en el mismo orden que ya se ve
 * (pasa cuando dos columnas van en paralelo), empieza por el sentido contrario: cada pulsación se nota.
 */
export function nextVisibleSort(
  positions: readonly PositionDto[],
  current: { key: PositionSortKey; dir: SortDir },
  pressed: PositionSortKey,
): { key: PositionSortKey; dir: SortDir } {
  const next = nextSort(current, pressed);
  if (next.key === current.key) return next;
  const ids = (sort: { key: PositionSortKey; dir: SortDir }) => sortPositions(positions, sort.key, sort.dir).map((p) => p.assetId).join();
  return ids(next) === ids(current) ? { key: pressed, dir: next.dir === "asc" ? "desc" : "asc" } : next;
}

/** Al pulsar una cabecera: la misma columna invierte el orden; una columna nueva empieza descendente (nombre, ascendente). */
export function nextSort(
  current: { key: PositionSortKey; dir: SortDir },
  pressed: PositionSortKey,
): { key: PositionSortKey; dir: SortDir } {
  if (current.key === pressed) return { key: pressed, dir: current.dir === "asc" ? "desc" : "asc" };
  return { key: pressed, dir: pressed === "name" ? "asc" : "desc" };
}
