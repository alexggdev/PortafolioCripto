"use client";

import { useRouter } from "next/navigation";
import { useTransition, type ChangeEvent } from "react";
import { useCurrency } from "@/features/currency/components/CurrencyProvider";
import { CURRENCY_COOKIE, CURRENCY_NAMES, isCurrencyCode } from "@/features/currency/lib/currency";

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

/** Elige la moneda de visualización. Se guarda en una cookie y el servidor vuelve a pintar con ella. */
export function CurrencySelect() {
  const { currency, available } = useCurrency();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  // Sin tipos de cambio (API sin ellos todavía) solo hay dólares: no tiene sentido mostrar el selector.
  if (available.length < 2) return null;

  function onChange(event: ChangeEvent<HTMLSelectElement>) {
    const next = event.target.value;
    if (!isCurrencyCode(next)) return;
    document.cookie = `${CURRENCY_COOKIE}=${next}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax`;
    startTransition(() => router.refresh());
  }

  return (
    <label className="flex items-center gap-2 text-sm text-soft">
      <span className="sr-only">Moneda en la que se muestran los precios</span>
      <select
        value={currency}
        onChange={onChange}
        aria-busy={pending}
        className={`border border-rule bg-surface px-2 py-1.5 font-medium text-ink ${pending ? "opacity-60" : ""}`}
      >
        {available.map((code) => (
          <option key={code} value={code}>
            {code} · {CURRENCY_NAMES[code].label}
          </option>
        ))}
      </select>
    </label>
  );
}
