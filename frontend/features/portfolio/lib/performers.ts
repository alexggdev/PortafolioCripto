import type { PositionDto } from "../../../shared/lib/types";

export interface Performer {
  slug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
  pnlUsd: number;
  pnlPct: number;
}

/** Moneda con mejor y peor rendimiento (por % de ganancia no realizada). Ignora las que no tienen precio. */
export function bestAndWorst(positions: readonly PositionDto[]): { best: Performer | null; worst: Performer | null } {
  const ranked: Performer[] = [];
  for (const p of positions) {
    if (p.unrealizedPnlUsd === null || p.unrealizedPnlPct === null) continue;
    ranked.push({
      slug: p.slug,
      symbol: p.symbol.toUpperCase(),
      name: p.name,
      imageUrl: p.imageUrl,
      pnlUsd: p.unrealizedPnlUsd,
      pnlPct: p.unrealizedPnlPct,
    });
  }
  if (ranked.length === 0) return { best: null, worst: null };

  ranked.sort((a, b) => b.pnlPct - a.pnlPct);
  return { best: ranked[0]!, worst: ranked[ranked.length - 1]! };
}
