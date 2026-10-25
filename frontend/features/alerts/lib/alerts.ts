import { normalizeDecimal } from "../../portfolio/lib/transactions";
import type { AlertDirection, PriceAlert } from "../../../shared/lib/types";

// Lógica pura de las alertas de precio (sin React): se prueba con tsx.
// Las reglas repiten las del servidor (PriceAlertService); el servidor siempre vuelve a comprobarlas.

/** Aviso entre componentes: algo cambió en las alertas (la campana vuelve a contar). */
export const ALERTS_CHANGED_EVENT = "cripto:alerts-changed";

const MAX_INT_DIGITS = 10;
const MAX_SCALE = 18;

export const DIRECTION_OPTIONS: readonly { value: AlertDirection; label: string }[] = [
  { value: "Above", label: "Cuando suba hasta" },
  { value: "Below", label: "Cuando baje hasta" },
];

export function directionLabel(direction: AlertDirection): string {
  return direction === "Above" ? "Sube hasta" : "Baja hasta";
}

export interface AlertFormValues {
  assetSlug: string;
  direction: AlertDirection;
  price: string;
}

export type AlertFieldErrors = Partial<Record<"asset" | "price", string>>;

export interface AlertFormResult {
  errors: AlertFieldErrors;
  /** JSON listo para enviar: el precio se escribe tal cual, sin pasar por coma flotante. */
  body: string | null;
}

export function validateAlertForm(values: AlertFormValues): AlertFormResult {
  const errors: AlertFieldErrors = {};

  if (!values.assetSlug.trim()) errors.asset = "Elige una moneda.";

  const price = normalizeDecimal(values.price);
  if (values.price.trim() === "") errors.price = "Escribe el precio objetivo.";
  else if (price === null) errors.price = "El precio no es un número válido.";
  else if (price === "0") errors.price = "El precio objetivo debe ser mayor que cero.";
  else {
    const [integer, fraction = ""] = price.split(".");
    if (integer.length > MAX_INT_DIGITS) errors.price = "El precio es demasiado grande.";
    else if (fraction.length > MAX_SCALE) errors.price = `El precio admite como máximo ${MAX_SCALE} decimales.`;
  }

  if (Object.keys(errors).length > 0 || price === null) return { errors, body: null };

  return {
    errors,
    body: `{"assetSlug":${JSON.stringify(values.assetSlug.trim())},"direction":${JSON.stringify(values.direction)},"targetPriceUsd":${price}}`,
  };
}

/** Cuánto le falta al precio actual para llegar al objetivo, en %. Positivo = hay que subir; negativo = bajar. */
export function distanceToTargetPct(current: number | null, target: number): number | null {
  if (current === null || !Number.isFinite(current) || current <= 0) return null;
  return (target / current - 1) * 100;
}

/** Activas (las más recientes primero) y disparadas (la última disparada primero). */
export function splitAlerts(alerts: readonly PriceAlert[]): { active: PriceAlert[]; triggered: PriceAlert[] } {
  const active = alerts.filter((a) => a.triggeredAtUtc === null).sort((a, b) => b.id - a.id);
  const triggered = alerts
    .filter((a) => a.triggeredAtUtc !== null)
    .sort((a, b) => Date.parse(b.triggeredAtUtc ?? "") - Date.parse(a.triggeredAtUtc ?? "") || b.id - a.id);
  return { active, triggered };
}
