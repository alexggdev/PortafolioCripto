import type { ChartPoint, PortfolioHistoryPoint } from "../../../shared/lib/types";

// Compara el portafolio con Bitcoin. Sin React, para poder probarlo.

export interface Comparison {
  /** Milisegundos desde 1970 (UTC) de cada día. */
  times: number[];
  /** Rentabilidad del portafolio desde el primer día, en %, descontando lo que aportaste o sacaste. */
  portfolioPct: number[];
  /** Rentabilidad de Bitcoin desde el primer día, en %. */
  btcPct: number[];
  /** Ganancia total en USD de cada día: lo que vale menos lo que has aportado. */
  profitUsd: number[];
}

/**
 * Rentabilidad ponderada en el tiempo (la del portafolio sin el efecto de tus ingresos y retiradas):
 * cada día, el valor de hoy menos el dinero que entró hoy, dividido entre el valor de ayer.
 * Así, ingresar 1.000 € no cuenta como ganancia. Se supone que el dinero entra al cierre del día.
 * Empieza el primer día con valor; Bitcoin se mide desde ese mismo día.
 */
export function compareWithBtc(points: readonly PortfolioHistoryPoint[], btc: readonly ChartPoint[]): Comparison | null {
  const prices = new Map(btc.filter((p) => p.priceUsd > 0).map((p) => [p.date, p.priceUsd]));
  if (prices.size === 0) return null;

  const times: number[] = [];
  const portfolioPct: number[] = [];
  const btcPct: number[] = [];
  const profitUsd: number[] = [];

  let index = 100;
  let previous: PortfolioHistoryPoint | null = null;
  let lastBtc: number | null = null;
  let firstBtc: number | null = null;

  for (const point of points) {
    const price: number | null = prices.get(point.date) ?? lastBtc; // día sin dato de Bitcoin: se mantiene el último conocido
    if (price === null) continue;
    lastBtc = price;

    if (firstBtc === null) {
      if (point.valueUsd <= 0) continue; // aún no había nada invertido
      firstBtc = price;
    } else if (previous !== null && previous.valueUsd > 0) {
      const flow = point.netContributedUsd - previous.netContributedUsd;
      const ratio = (point.valueUsd - flow) / previous.valueUsd;
      if (Number.isFinite(ratio) && ratio > 0) index *= ratio;
    }

    if (firstBtc === null) continue; // no ocurre: ya se fijó arriba; sirve para que TypeScript lo sepa

    previous = point;
    times.push(Date.parse(`${point.date}T00:00:00Z`));
    portfolioPct.push(index - 100);
    btcPct.push((price / firstBtc) * 100 - 100);
    profitUsd.push(point.valueUsd - point.netContributedUsd);
  }

  return times.length >= 2 ? { times, portfolioPct, btcPct, profitUsd } : null;
}
