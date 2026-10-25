// Monedas en las que se puede mostrar la web. Todo se guarda y calcula en USD; esto solo convierte al mostrar.

export const CURRENCY_CODES = ["USD", "EUR", "PEN", "CLP"] as const;
export type CurrencyCode = (typeof CURRENCY_CODES)[number];

export const DEFAULT_CURRENCY: CurrencyCode = "USD";
export const CURRENCY_COOKIE = "currency";

export const CURRENCY_NAMES: Record<CurrencyCode, { label: string; plural: string }> = {
  USD: { label: "Dólar", plural: "dólares" },
  EUR: { label: "Euro", plural: "euros" },
  PEN: { label: "Sol peruano", plural: "soles" },
  CLP: { label: "Peso chileno", plural: "pesos chilenos" },
};

/** Unidades de cada moneda por 1 USD. USD siempre está y vale 1. */
export interface FxRates {
  rates: Partial<Record<CurrencyCode, number>>;
  /** ISO 8601 (UTC) de la última actualización del proveedor; null si no se sabe. */
  updatedAtUtc: string | null;
}

export const USD_ONLY: FxRates = { rates: { USD: 1 }, updatedAtUtc: null };

export function isCurrencyCode(value: unknown): value is CurrencyCode {
  return typeof value === "string" && (CURRENCY_CODES as readonly string[]).includes(value);
}

/** Lo que llegue en la cookie, convertido en una moneda conocida (si no, USD). */
export function parseCurrency(value: string | null | undefined): CurrencyCode {
  const wanted = (value ?? "").trim().toUpperCase();
  return isCurrencyCode(wanted) ? wanted : DEFAULT_CURRENCY;
}

/** Valida la respuesta de /api/fx: se queda solo con monedas conocidas y tipos positivos y finitos. */
export function sanitizeRates(raw: unknown): FxRates {
  if (typeof raw !== "object" || raw === null) return USD_ONLY;
  const input = raw as { rates?: unknown; updatedAtUtc?: unknown };
  if (typeof input.rates !== "object" || input.rates === null) return USD_ONLY;

  const rates: Partial<Record<CurrencyCode, number>> = { USD: 1 };
  for (const code of CURRENCY_CODES) {
    if (code === "USD") continue;
    const value = (input.rates as Record<string, unknown>)[code];
    if (typeof value === "number" && Number.isFinite(value) && value > 0) rates[code] = value;
  }

  const updatedAtUtc =
    typeof input.updatedAtUtc === "string" && !Number.isNaN(Date.parse(input.updatedAtUtc)) ? input.updatedAtUtc : null;
  return { rates, updatedAtUtc };
}

export function availableCurrencies(fx: FxRates): CurrencyCode[] {
  return CURRENCY_CODES.filter((code) => fx.rates[code] !== undefined);
}

/** La moneda pedida con su tipo de cambio; si no hay tipo, cae a USD y lo dice en `fallback`. */
export function resolveCurrency(
  requested: CurrencyCode,
  fx: FxRates,
): { currency: CurrencyCode; rate: number; fallback: boolean } {
  const rate = fx.rates[requested];
  if (rate === undefined) return { currency: DEFAULT_CURRENCY, rate: 1, fallback: requested !== DEFAULT_CURRENCY };
  return { currency: requested, rate, fallback: false };
}
