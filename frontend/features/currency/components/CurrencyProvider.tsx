"use client";

import { createContext, useContext, useMemo, type ReactNode } from "react";
import {
  availableCurrencies,
  resolveCurrency,
  type CurrencyCode,
  type FxRates,
} from "@/features/currency/lib/currency";
import { formatMoney, formatMoneyValue, formatMoneyWhole, formatSignedMoney } from "@/features/currency/lib/money";

interface CurrencyState {
  /** La moneda que se está mostrando de verdad. */
  currency: CurrencyCode;
  /** La que pidió el visitante (puede diferir si falta su tipo de cambio). */
  requested: CurrencyCode;
  fallback: boolean;
  rate: number;
  fx: FxRates;
  available: CurrencyCode[];
}

const Ctx = createContext<CurrencyState | null>(null);

export function CurrencyProvider({
  requested,
  fx,
  children,
}: {
  requested: CurrencyCode;
  fx: FxRates;
  children: ReactNode;
}) {
  const value = useMemo<CurrencyState>(() => {
    const resolved = resolveCurrency(requested, fx);
    return { ...resolved, requested, fx, available: availableCurrencies(fx) };
  }, [requested, fx]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useCurrency(): CurrencyState {
  const value = useContext(Ctx);
  if (!value) throw new Error("useCurrency necesita un CurrencyProvider");
  return value;
}

/** Formateadores ya ligados a la moneda elegida. `price`/`whole` reciben USD; `fmt` recibe un valor ya convertido. */
export function useMoney() {
  const { currency, rate } = useCurrency();
  return useMemo(
    () => ({
      currency,
      rate,
      price: (usd: number | null | undefined) => formatMoney(usd, currency, rate),
      signed: (usd: number | null | undefined) => formatSignedMoney(usd, currency, rate),
      whole: (usd: number | null | undefined) => formatMoneyWhole(usd, currency, rate),
      fmt: (converted: number | null | undefined) => formatMoneyValue(converted, currency),
    }),
    [currency, rate],
  );
}
