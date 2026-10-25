import type { PortfolioValue, PositionDto } from "../../../shared/lib/types";

export interface AllocationSlice {
  key: string;
  label: string;
  symbol: string;
  valueUsd: number;
  /** Porcentaje del total (0-100). */
  pct: number;
}

/**
 * Reparto del portafolio por moneda para el gráfico de asignación.
 * Solo cuentan las monedas con precio y valor positivo. Las más pequeñas se agrupan en «Otras».
 */
export function buildAllocation(positions: readonly PositionDto[], maxSlices = 6): AllocationSlice[] {
  const priced = positions
    .filter((p): p is PositionDto & { valueUsd: number } => p.valueUsd !== null && p.valueUsd > 0)
    .sort((a, b) => b.valueUsd - a.valueUsd);

  const total = priced.reduce((sum, p) => sum + p.valueUsd, 0);
  if (total <= 0) return [];

  const toSlice = (p: PositionDto & { valueUsd: number }): AllocationSlice => ({
    key: p.slug,
    label: p.name,
    symbol: p.symbol.toUpperCase(),
    valueUsd: p.valueUsd,
    pct: (p.valueUsd / total) * 100,
  });

  // Agrupar una sola moneda en «Otras» no ahorra nada: se deja tal cual.
  if (priced.length <= maxSlices + 1) return priced.map(toSlice);

  const head = priced.slice(0, maxSlices).map(toSlice);
  const restValue = priced.slice(maxSlices).reduce((sum, p) => sum + p.valueUsd, 0);
  return [...head, { key: "otras", label: "Otras", symbol: "", valueUsd: restValue, pct: (restValue / total) * 100 }];
}

/** Reparto de la vista general por portafolio (en vez de por moneda). Ignora los vacíos. */
export function buildPortfolioAllocation(portfolios: readonly PortfolioValue[]): AllocationSlice[] {
  const filled = portfolios.filter((p) => p.valueUsd > 0).sort((a, b) => b.valueUsd - a.valueUsd);
  const total = filled.reduce((sum, p) => sum + p.valueUsd, 0);
  if (total <= 0) return [];
  return filled.map((p) => ({ key: p.id, label: p.name, symbol: "", valueUsd: p.valueUsd, pct: (p.valueUsd / total) * 100 }));
}

/** «0,79 %», o «<0,01 %» si es casi nada. */
export function formatSlicePct(pct: number): string {
  return pct < 0.01 ? "<0,01 %" : `${pct.toFixed(2).replace(".", ",")} %`;
}
