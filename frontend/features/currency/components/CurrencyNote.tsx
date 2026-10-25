"use client";

import { useCurrency } from "@/features/currency/components/CurrencyProvider";
import { formatMoneyValue } from "@/features/currency/lib/money";

const dateFormat = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

/** Aviso bajo los precios cuando no están en dólares: de dónde sale la conversión y qué no se convierte. */
export function CurrencyNote() {
  const { currency, requested, fallback, rate, fx } = useCurrency();

  if (fallback) {
    return (
      <p role="status" className="mt-3 text-sm text-down">
        No se pudo cargar el tipo de cambio de {requested}, así que los precios se muestran en dólares (USD).
      </p>
    );
  }
  if (currency === "USD") return null;

  const date = fx.updatedAtUtc ? ` (${dateFormat.format(new Date(fx.updatedAtUtc))})` : "";
  return (
    <p className="mt-2 text-xs text-soft">
      Convertido desde dólares: 1 US$ = {formatMoneyValue(rate, currency)}
      {date}.
    </p>
  );
}
