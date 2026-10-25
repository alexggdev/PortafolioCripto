import { paddedDomain } from "../../../shared/lib/chart";
import type { PortfolioHistory, PortfolioHistoryPoint } from "../../../shared/lib/types";

// Preparación de los datos de la gráfica del portafolio. Sin React, para poder probarla.

export interface HistorySeries {
  /** Milisegundos desde 1970 (UTC) de cada día. */
  times: number[];
  /** Valor del portafolio, ya convertido a la moneda elegida. */
  value: number[];
  /** Dinero neto aportado, ya convertido. */
  contributed: number[];
}

/** USD → moneda elegida, punto a punto. */
export function toHistorySeries(points: readonly PortfolioHistoryPoint[], rate: number): HistorySeries {
  return {
    times: points.map((p) => Date.parse(`${p.date}T00:00:00Z`)),
    value: points.map((p) => p.valueUsd * rate),
    contributed: points.map((p) => p.netContributedUsd * rate),
  };
}

/**
 * Dominio vertical que abarca las dos líneas. Si todo es positivo se comporta como el gráfico de precios (suelo en 0);
 * lo aportado puede ser negativo (vendiste por más de lo que pusiste), y entonces el suelo baja con él.
 */
export function historyDomain(...lines: number[][]): [number, number] {
  const all = lines.flat().filter((v) => Number.isFinite(v));
  if (all.length === 0) return [0, 1];
  const min = Math.min(...all);
  if (min >= 0) return paddedDomain(all);

  const max = Math.max(...all);
  const pad = (max - min) * 0.08 || Math.abs(min) * 0.01 || 1;
  return [min - pad, max + pad];
}

/**
 * Ganancia del periodo en %, aproximada: ganancia ÷ (valor al empezar + dinero añadido durante el periodo).
 * No pondera por tiempo; sirve como orden de magnitud. Null si la base no es positiva.
 */
export function periodProfitPct(points: readonly PortfolioHistoryPoint[], profitUsd: number | null): number | null {
  if (profitUsd === null || points.length < 2) return null;
  const first = points[0];
  const last = points[points.length - 1];
  const base = first.valueUsd + Math.max(0, last.netContributedUsd - first.netContributedUsd);
  return base > 0 ? (profitUsd / base) * 100 : null;
}

/** Diferencia entre lo que vale y lo que aportaste: la revalorización acumulada. */
export function gainAt(series: HistorySeries, index: number): number | null {
  if (index < 0 || index >= series.value.length) return null;
  return series.value[index] - series.contributed[index];
}

/**
 * Suma las historias de varios portafolios en una sola (la «Vista general»). Cada día vale lo que valía cada portafolio
 * ese día; uno que todavía no existía aporta 0, y uno sin dato ese día conserva el último conocido.
 */
export function sumHistories(histories: readonly PortfolioHistory[]): PortfolioHistory | null {
  const first = histories[0];
  if (!first) return null;

  const dates = [...new Set(histories.flatMap((h) => h.points.map((p) => p.date)))].sort();
  const cursors = histories.map(() => 0);
  const latest: (PortfolioHistoryPoint | null)[] = histories.map(() => null);

  const points = dates.map((date) => {
    let valueUsd = 0;
    let netContributedUsd = 0;
    histories.forEach((h, i) => {
      while (cursors[i] < h.points.length && h.points[cursors[i]].date <= date) latest[i] = h.points[cursors[i]++];
      valueUsd += latest[i]?.valueUsd ?? 0;
      netContributedUsd += latest[i]?.netContributedUsd ?? 0;
    });
    return { date, valueUsd, netContributedUsd };
  });

  const profits = histories.map((h) => h.periodProfitUsd).filter((v): v is number => v !== null);
  const firstDates = histories.map((h) => h.firstTransactionDate).filter((d): d is string => d !== null).sort();
  return {
    portfolioId: null,
    range: first.range,
    firstTransactionDate: firstDates[0] ?? null,
    points,
    periodProfitUsd: profits.length > 0 ? profits.reduce((a, b) => a + b, 0) : null,
    includesCurrentValue: histories.some((h) => h.includesCurrentValue),
  };
}

/** Color de la gráfica: verde si en el periodo hubo ganancia (o no se sabe), rojo si hubo pérdida. */
export function profitTone(periodProfitUsd: number | null | undefined): "up" | "down" {
  return periodProfitUsd !== null && periodProfitUsd !== undefined && periodProfitUsd < 0 ? "down" : "up";
}
