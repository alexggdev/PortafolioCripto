"use client";

import { useEffect, useState } from "react";
import type { RangeCode } from "@/shared/lib/types";

const HIDE_KEY = "cp_hide_balances";

// El contenedor principal lleva `group` y `data-hide`: con los saldos ocultos, todo lo marcado con esta clase se difumina.
export const SENSITIVE = "group-data-[hide=true]:blur-md group-data-[hide=true]:select-none";
export const CARD = "rounded-2xl bg-surface p-4";

export const RANGE_BUTTONS: readonly { code: RangeCode; text: string }[] = [
  { code: "7D", text: "7d" },
  { code: "1M", text: "30d" },
  { code: "3M", text: "90d" },
  { code: "1Y", text: "1a" },
  { code: "All", text: "Todo" },
];

/** «Ocultar saldos»: preferencia del navegador. Se lee tras montar para que servidor y cliente coincidan. */
export function useHideBalances(): [boolean, () => void] {
  const [hidden, setHidden] = useState(false);

  useEffect(() => {
    try {
      setHidden(window.localStorage.getItem(HIDE_KEY) === "1");
    } catch {
      /* sin almacenamiento: se queda visible */
    }
  }, []);

  function toggle() {
    const next = !hidden;
    setHidden(next);
    try {
      window.localStorage.setItem(HIDE_KEY, next ? "1" : "0");
    } catch {
      /* no se pudo recordar: solo vale para esta visita */
    }
  }

  return [hidden, toggle];
}

export function EyeButton({ hidden, onToggle }: { hidden: boolean; onToggle: () => void }) {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={hidden}
      aria-label={hidden ? "Mostrar saldos" : "Ocultar saldos"}
      title={hidden ? "Mostrar saldos" : "Ocultar saldos"}
      className="inline-flex size-9 items-center justify-center text-soft hover:text-ink"
    >
      <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        {hidden ? (
          <>
            <path d="M3 3l18 18" />
            <path d="M10.6 6.1A9.8 9.8 0 0 1 12 6c5 0 8.5 4.2 9.5 6a12 12 0 0 1-2.6 3.2M6.5 7.6A12.4 12.4 0 0 0 2.5 12c1 1.8 4.5 6 9.5 6 1.4 0 2.7-.3 3.8-.8" />
            <path d="M9.9 9.9a3 3 0 0 0 4.2 4.2" />
          </>
        ) : (
          <>
            <path d="M2.5 12C3.5 10.2 7 6 12 6s8.5 4.2 9.5 6c-1 1.8-4.5 6-9.5 6S3.5 13.8 2.5 12z" />
            <circle cx="12" cy="12" r="3" />
          </>
        )}
      </svg>
    </button>
  );
}

/** Botones agrupados (7d · 30d…, Token · Portafolio): el activo se marca con fondo, no solo con color. */
export function Segmented<T extends string>({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: T;
  options: readonly { code: T; text: string }[];
  onChange: (code: T) => void;
}) {
  return (
    <div role="group" aria-label={label} className="inline-flex rounded-lg bg-paper p-1">
      {options.map((o) => (
        <button
          key={o.code}
          type="button"
          aria-pressed={o.code === value}
          onClick={() => onChange(o.code)}
          className={`cursor-pointer rounded-md px-3 py-1.5 text-sm font-semibold transition-colors ${o.code === value ? "bg-surface text-ink shadow-sm" : "text-soft hover:text-ink"}`}
        >
          {o.text}
        </button>
      ))}
    </div>
  );
}
