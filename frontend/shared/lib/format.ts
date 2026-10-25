// Formato de números para mostrar (es-ES). Todo se guarda y calcula en USD; aquí solo se presenta.

const LOCALE = "es-ES";
const MINUS = "−"; // signo menos tipográfico
const NBSP = " ";
const EM_DASH = "—";

const currencyFormats = new Map<number, Intl.NumberFormat>();

function currency(maxDecimals: number): Intl.NumberFormat {
  let format = currencyFormats.get(maxDecimals);
  if (!format) {
    format = new Intl.NumberFormat(LOCALE, {
      style: "currency",
      currency: "USD",
      minimumFractionDigits: Math.min(2, maxDecimals),
      maximumFractionDigits: maxDecimals,
      useGrouping: "always", // en español 1234 no se agrupa por defecto; aquí sí: 1.234
    });
    currencyFormats.set(maxDecimals, format);
  }
  return format;
}

const percentFormat = new Intl.NumberFormat(LOCALE, {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  useGrouping: "always",
});

/** Decimales según el tamaño del precio: una moneda de céntimos necesita más que una de miles. */
export function decimalsFor(value: number): number {
  const v = Math.abs(value);
  if (v >= 1) return 2;
  if (v >= 0.1) return 4;
  if (v >= 0.01) return 5;
  if (v >= 0.0001) return 6;
  return 8;
}

/** Precio unitario: 83.554,12 US$ · 0,00001234 US$ */
export function formatUsd(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EM_DASH;
  return currency(decimalsFor(value)).format(value);
}

/** Importes grandes (capitalización, volumen), enteros y completos para no confundir "B" y "mil millones". */
export function formatUsdWhole(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EM_DASH;
  return currency(0).format(value);
}

export type Direction = "up" | "down" | "flat";

export function directionOf(value: number | null | undefined): Direction {
  if (value === null || value === undefined || !Number.isFinite(value)) return "flat";
  // Lo que se muestra con dos decimales como 0,00 no es ni subida ni bajada.
  const rounded = Math.round(value * 100) / 100;
  if (rounded > 0) return "up";
  if (rounded < 0) return "down";
  return "flat";
}

/** Variación con signo explícito: +1,23 % · −4,50 % · 0,00 % */
export function formatPct(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EM_DASH;
  const direction = directionOf(value);
  const body = percentFormat.format(Math.abs(value));
  const sign = direction === "up" ? "+" : direction === "down" ? MINUS : "";
  return `${sign}${body}${NBSP}%`;
}

/** "hace 4 s", "hace 2 min", "hace 3 h", "hace 2 d". */
export function formatAgo(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  if (s < 60) return `hace ${s}${NBSP}s`;
  const min = Math.floor(s / 60);
  if (min < 60) return `hace ${min}${NBSP}min`;
  const h = Math.floor(min / 60);
  if (h < 48) return `hace ${h}${NBSP}h`;
  return `hace ${Math.floor(h / 24)}${NBSP}d`;
}

export const NO_VALUE = EM_DASH;

const quantityFormat = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 8, useGrouping: "always" });
const tinyQuantityFormat = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 12, useGrouping: "always" });

/** Cantidad de monedas: hasta 8 decimales sin ceros sobrantes (0,5 · 1.250 · 0,00012345). */
export function formatQuantity(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EM_DASH;
  return (Math.abs(value) < 0.00000001 && value !== 0 ? tinyQuantityFormat : quantityFormat).format(value);
}

/** Número sin notación científica ni separador de miles, para rellenar un campo de texto. */
export function toPlainNumber(value: number): string {
  return value.toLocaleString("en-US", { useGrouping: false, maximumFractionDigits: 12 });
}
