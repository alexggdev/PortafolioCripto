// Matemática del gráfico de precios: escalas, ticks y formato. Sin React, para poder probarla.

export interface SeriesPoint {
  /** Milisegundos desde 1970 (UTC). */
  t: number;
  /** Precio en USD. */
  v: number;
}

export interface Ticks {
  values: number[];
  /** Separación entre ticks (sirve para decidir cuántos decimales mostrar). */
  step: number;
}

/** Ticks "redondos" (1, 2, 5 × 10ⁿ) dentro de [min, max]. */
export function niceTicks(min: number, max: number, target = 5): Ticks {
  if (!Number.isFinite(min) || !Number.isFinite(max) || max <= min) {
    return { values: Number.isFinite(min) ? [min] : [], step: 1 };
  }

  const rough = (max - min) / Math.max(1, target);
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const normalized = rough / magnitude;
  const step = (normalized < 1.5 ? 1 : normalized < 3 ? 2 : normalized < 7 ? 5 : 10) * magnitude;

  const decimals = Math.max(0, 1 - Math.floor(Math.log10(step)));
  const values: number[] = [];
  for (let value = Math.ceil(min / step) * step; value <= max + step * 1e-9; value += step) {
    values.push(Number(value.toFixed(decimals)));
  }
  return { values, step };
}

/** Dominio vertical con aire arriba y abajo. Un precio no puede ser negativo, así que el suelo es 0. */
export function paddedDomain(values: number[]): [number, number] {
  let min = Infinity;
  let max = -Infinity;
  for (const v of values) {
    if (v < min) min = v;
    if (v > max) max = v;
  }
  if (!Number.isFinite(min)) return [0, 1];

  const pad = max > min ? (max - min) * 0.08 : Math.abs(max) * 0.01 || 1;
  return [Math.max(0, min - pad), max + pad];
}

export function scaleLinear(domain: [number, number], range: [number, number]): (value: number) => number {
  const [d0, d1] = domain;
  const [r0, r1] = range;
  const span = d1 - d0;
  return (value) => (span === 0 ? (r0 + r1) / 2 : r0 + ((value - d0) / span) * (r1 - r0));
}

/** Trazado "M x,y L x,y …" con un decimal (suficiente para píxeles y mucho más corto). */
export function linePath(xs: number[], ys: number[]): string {
  return xs.map((x, i) => `${i === 0 ? "M" : "L"}${x.toFixed(1)},${ys[i].toFixed(1)}`).join(" ");
}

/** Área bajo la línea, cerrada contra la base del gráfico. */
export function areaPath(xs: number[], ys: number[], baseline: number): string {
  if (xs.length === 0) return "";
  return `${linePath(xs, ys)} L${xs[xs.length - 1].toFixed(1)},${baseline.toFixed(1)} L${xs[0].toFixed(1)},${baseline.toFixed(1)} Z`;
}

/** Índice del punto más cercano a x (xs está ordenado de menor a mayor). */
export function nearestIndex(xs: number[], x: number): number {
  if (xs.length === 0) return -1;
  let lo = 0;
  let hi = xs.length - 1;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    if (xs[mid] < x) lo = mid + 1;
    else hi = mid;
  }
  if (lo > 0 && Math.abs(xs[lo - 1] - x) <= Math.abs(xs[lo] - x)) return lo - 1;
  return lo;
}

const DAY_MS = 86_400_000;

const dayMonth = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "short", timeZone: "UTC" });
const monthYear = new Intl.DateTimeFormat("es-ES", { month: "short", year: "numeric", timeZone: "UTC" });
const yearOnly = new Intl.DateTimeFormat("es-ES", { year: "numeric", timeZone: "UTC" });
const longDate = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

/** Etiqueta del eje X según lo larga que sea la serie: "12 oct", "oct 2026" o "2026". */
export function formatTickDate(t: number, spanMs: number): string {
  if (spanMs <= 100 * DAY_MS) return dayMonth.format(t);
  if (spanMs <= 800 * DAY_MS) return monthYear.format(t);
  return yearOnly.format(t);
}

/** "12 de octubre de 2026". */
export function formatLongDate(t: number): string {
  return longDate.format(t);
}

/** Fechas repartidas por igual entre el primer y el último punto. */
export function pickDateTicks(t0: number, t1: number, count: number): number[] {
  if (count <= 1 || t1 <= t0) return [t0];
  return Array.from({ length: count }, (_, i) => t0 + ((t1 - t0) * i) / (count - 1));
}

const axisFormats = new Map<number, Intl.NumberFormat>();

/** Número del eje Y sin símbolo de moneda (el gráfico ya dice que son dólares). */
export function formatAxisNumber(value: number, step: number): string {
  const decimals = step >= 1 ? 0 : Math.min(8, Math.ceil(-Math.log10(step) - 1e-9));
  let format = axisFormats.get(decimals);
  if (!format) {
    format = new Intl.NumberFormat("es-ES", {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
      useGrouping: "always",
    });
    axisFormats.set(decimals, format);
  }
  return format.format(value);
}

const shortDate = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });

/** "20 oct 2026". */
export function formatShortDate(t: number): string {
  return shortDate.format(t);
}

/**
 * Etiquetas compactas del eje Y con símbolo de moneda ("$10.4K", "$4.2M"). Usa los decimales justos para que no
 * salgan dos etiquetas iguales seguidas ($4.2M, $4.2M…).
 */
export function compactAxisLabels(values: readonly number[], currency: string): string[] {
  try {
    let labels: string[] = [];
    for (let digits = 0; digits <= 3; digits++) {
      const format = new Intl.NumberFormat("en-US", { style: "currency", currency, notation: "compact", maximumFractionDigits: digits });
      labels = values.map((v) => format.format(v));
      if (new Set(labels).size === labels.length) break;
    }
    return labels;
  } catch {
    return values.map((v) => String(v));
  }
}

/** Eje de porcentajes: 0%, 300%, 1.5K%, -300%. */
export function formatPctAxis(value: number): string {
  const abs = Math.abs(value);
  const trim = (n: number) => String(Number(n.toFixed(1)));
  const text = abs >= 1000 ? `${trim(abs / 1000)}K` : trim(abs);
  return `${value < 0 ? "-" : ""}${text}%`;
}
