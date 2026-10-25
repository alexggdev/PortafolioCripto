import { decimalsFor, directionOf, NO_VALUE } from "../../../shared/lib/format";
import type { CurrencyCode } from "./currency";

// Dinero para mostrar (es-ES) en cualquier moneda soportada. Los importes llegan de la API en USD.

const LOCALE = "es-ES";
const formats = new Map<string, Intl.NumberFormat>();

function currencyFormat(currency: CurrencyCode, maxDecimals: number): Intl.NumberFormat {
  const key = `${currency}:${maxDecimals}`;
  let format = formats.get(key);
  if (!format) {
    format = new Intl.NumberFormat(LOCALE, {
      style: "currency",
      currency,
      minimumFractionDigits: Math.min(2, maxDecimals),
      maximumFractionDigits: maxDecimals,
      useGrouping: "always", // en español 1234 no se agrupa por defecto; aquí sí: 1.234
    });
    formats.set(key, format);
  }
  return format;
}

/** Los pesos chilenos no usan céntimos: de 100 pesos para arriba se muestran enteros. */
function unitDecimals(value: number, currency: CurrencyCode): number {
  if (currency === "CLP" && Math.abs(value) >= 100) return 0;
  return decimalsFor(value);
}

/** USD → moneda elegida (multiplicando por el tipo de cambio). null si no hay valor. */
export function convertUsd(usd: number | null | undefined, rate: number): number | null {
  if (usd === null || usd === undefined || !Number.isFinite(usd)) return null;
  return usd * rate;
}

/** Precio unitario ya convertido: 71.900,50 € · 0,00001234 US$ · 79.480.000 CLP */
export function formatMoneyValue(value: number | null | undefined, currency: CurrencyCode): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return NO_VALUE;
  return currencyFormat(currency, unitDecimals(value, currency)).format(value);
}

/** Importe grande ya convertido (capitalización, volumen): entero y completo. */
export function formatMoneyWholeValue(value: number | null | undefined, currency: CurrencyCode): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return NO_VALUE;
  return currencyFormat(currency, 0).format(value);
}

export function formatMoney(usd: number | null | undefined, currency: CurrencyCode, rate: number): string {
  return formatMoneyValue(convertUsd(usd, rate), currency);
}

export function formatMoneyWhole(usd: number | null | undefined, currency: CurrencyCode, rate: number): string {
  return formatMoneyWholeValue(convertUsd(usd, rate), currency);
}

/** Ganancia o pérdida con signo explícito: +1.234,50 € · −80,00 € · 0,00 € (cero sin signo). */
export function formatSignedMoney(usd: number | null | undefined, currency: CurrencyCode, rate: number): string {
  const value = convertUsd(usd, rate);
  if (value === null) return NO_VALUE;
  const direction = directionOf(value);
  const body = formatMoneyValue(Math.abs(value), currency);
  return direction === "up" ? `+${body}` : direction === "down" ? `\u2212${body}` : body;
}
