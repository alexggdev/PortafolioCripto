"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { logoutAction } from "@/app/actions/auth";
import { useCurrency } from "@/features/currency/components/CurrencyProvider";
import { CheckIcon, ChevronRightIcon } from "@/shared/components/Icons";
import { CURRENCY_COOKIE, CURRENCY_NAMES, isCurrencyCode } from "@/features/currency/lib/currency";

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;
const SYMBOLS = { USD: "$", EUR: "€", PEN: "S/", CLP: "$" } as const;
type ThemeChoice = "light" | "dark" | "system";
const THEMES: readonly { code: ThemeChoice; text: string }[] = [
  { code: "light", text: "Claro" },
  { code: "dark", text: "Oscuro" },
  { code: "system", text: "Sistema" },
];

function readTheme(): ThemeChoice {
  const stored = document.documentElement.dataset.theme;
  return stored === "light" || stored === "dark" ? stored : "system";
}

/** Nombre corto para el saludo: lo que va antes de la @ del correo. */
export function greetingName(email: string): string {
  const local = email.split("@")[0]?.trim() ?? "";
  return local || "usuario";
}

/**
 * Menú de usuario como el de CoinMarketCap: saludo y correo, moneda (lista que se despliega), tema
 * (Claro · Oscuro · Sistema) y cerrar sesión. «Sistema» sigue el tema del equipo.
 */
export function UserMenu({ email }: { email: string }) {
  const [open, setOpen] = useState(false);
  const [currencyOpen, setCurrencyOpen] = useState(false);
  const [theme, setTheme] = useState<ThemeChoice>("system");
  const { currency, available } = useCurrency();
  const router = useRouter();
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    setTheme(readTheme());
    function onPointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false);
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  function chooseTheme(next: ThemeChoice) {
    const root = document.documentElement;
    if (next === "system") {
      delete root.dataset.theme;
      document.cookie = "theme=; path=/; max-age=0; samesite=lax";
    } else {
      root.dataset.theme = next;
      document.cookie = `theme=${next}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax`;
    }
    setTheme(next);
  }

  function chooseCurrency(code: string) {
    if (!isCurrencyCode(code)) return;
    document.cookie = `${CURRENCY_COOKIE}=${code}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax`;
    setCurrencyOpen(false);
    router.refresh();
  }

  const name = greetingName(email);
  const initial = (name[0] ?? "?").toUpperCase();

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Menú de usuario"
        className="inline-flex size-10 items-center justify-center rounded-full bg-accent text-sm font-bold text-on-accent"
      >
        {initial}
      </button>

      {open && (
        <div role="menu" className="absolute right-0 z-40 mt-3 w-80 max-w-[calc(100vw-2rem)] rounded-2xl border border-rule bg-surface p-4 text-ink shadow-2xl">
          <div className="flex items-center gap-3 border-b border-rule pb-4">
            <span aria-hidden="true" className="inline-flex size-12 shrink-0 items-center justify-center rounded-full bg-accent text-lg font-bold text-on-accent">
              {initial}
            </span>
            <div className="min-w-0">
              <p className="truncate font-bold">Hola, {name}</p>
              <p className="truncate text-sm text-soft" title={email}>
                {email}
              </p>
            </div>
          </div>

          <div className="border-b border-rule py-3">
            <button
              type="button"
              onClick={() => setCurrencyOpen((o) => !o)}
              aria-expanded={currencyOpen}
              disabled={available.length < 2}
              className="flex w-full items-center justify-between rounded-lg py-2 text-left font-semibold disabled:cursor-default"
            >
              <span>Moneda</span>
              <span className="inline-flex items-center gap-2 font-medium text-soft">
                <span className="inline-flex size-5 items-center justify-center rounded-full bg-up text-[10px] font-bold text-on-accent" aria-hidden="true">
                  {SYMBOLS[currency]}
                </span>
                {currency}
                {available.length > 1 && <ChevronRightIcon className={`size-4 transition-transform ${currencyOpen ? "rotate-90" : ""}`} />}
              </span>
            </button>
            {currencyOpen && (
              <ul className="mt-1 space-y-0.5" aria-label="Elige la moneda">
                {available.map((code) => (
                  <li key={code}>
                    <button
                      type="button"
                      onClick={() => chooseCurrency(code)}
                      aria-pressed={code === currency}
                      className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm hover:bg-paper"
                    >
                      <span>
                        <span className="font-semibold">{code}</span> <span className="text-soft">{CURRENCY_NAMES[code].label}</span>
                      </span>
                      {code === currency && <CheckIcon className="size-4 text-accent" />}
                    </button>
                  </li>
                ))}
              </ul>
            )}

            <div className="mt-2 flex items-center justify-between gap-3 py-2">
              <span className="font-semibold">Tema</span>
              <div role="group" aria-label="Tema" className="inline-flex rounded-lg bg-paper p-1">
                {THEMES.map((t) => (
                  <button
                    key={t.code}
                    type="button"
                    aria-pressed={theme === t.code}
                    onClick={() => chooseTheme(t.code)}
                    className={`rounded-md px-3 py-1.5 text-sm font-semibold ${theme === t.code ? "bg-surface text-ink shadow-sm" : "text-soft hover:text-ink"}`}
                  >
                    {t.text}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <form action={logoutAction} className="pt-3">
            <button type="submit" className="w-full rounded-lg px-1 py-2 text-left font-semibold hover:text-down">
              Cerrar sesión
            </button>
          </form>
        </div>
      )}
    </div>
  );
}
