/** «$95.81B»: cifra grande abreviada, en la moneda mostrada (`rate` convierte desde USD). Sin dato, «--». */
export function formatCompactMoney(usd: number | null | undefined, currency: string, rate: number): string {
  if (usd === null || usd === undefined || !Number.isFinite(usd)) return "--";
  return new Intl.NumberFormat("en-US", { style: "currency", currency, notation: "compact", maximumFractionDigits: 2 }).format(usd * rate);
}

/** Puntos de una línea pequeña (sin ejes) dentro de `width`×`height`, con un margen para que el trazo no se corte. Menos de dos valores: null. */
export function sparklinePath(values: readonly number[], width: number, height: number, pad = 2): string | null {
  if (values.length < 2) return null;
  const min = Math.min(...values);
  const span = Math.max(...values) - min;
  return values
    .map((v, i) => {
      const x = (i / (values.length - 1)) * width;
      const y = span === 0 ? height / 2 : pad + (1 - (v - min) / span) * (height - pad * 2);
      return `${i === 0 ? "M" : "L"}${x.toFixed(1)} ${y.toFixed(1)}`;
    })
    .join(" ");
}
