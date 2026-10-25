"use client";

import { useCallback, useState } from "react";
import { DownloadIcon } from "@/shared/components/Icons";
import { PopPanel } from "@/shared/components/PopMenu";
import { exportHref } from "@/features/portfolio/lib/exportUrl";
import type { ExportKind } from "@/features/portfolio/lib/exportCsv";

const dateFormat = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

const OPTIONS: readonly { kind: ExportKind; title: string; withDate?: boolean }[] = [
  { kind: "history", title: "Historial de transacciones" },
  { kind: "overview", title: "Resumen del portafolio", withDate: true },
];

function Checkbox({ checked }: { checked: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`inline-flex size-6 shrink-0 items-center justify-center rounded-md border-2 transition-colors ${checked ? "border-accent bg-accent text-on-accent" : "border-soft/60"}`}
    >
      {checked && (
        <svg viewBox="0 0 24 24" className="size-4" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
          <path d="M5 12.5l4.5 4.5L19 7.5" />
        </svg>
      )}
    </span>
  );
}

/**
 * Botón «Exportar» como el de CoinMarketCap: un panel con dos casillas (historial y resumen) y «Exportar a CSV».
 * Con una casilla se descarga un CSV; con las dos, un ZIP con ambos.
 */
export function ExportMenu({ portfolioId }: { portfolioId: string | null }) {
  const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);
  const [selected, setSelected] = useState<ReadonlySet<ExportKind>>(new Set());
  const close = useCallback(() => setAnchor(null), []);

  const toggle = (kind: ExportKind) =>
    setSelected((current) => {
      const next = new Set(current);
      if (!next.delete(kind)) next.add(kind);
      return next;
    });

  function download() {
    const kinds = OPTIONS.filter((o) => selected.has(o.kind)).map((o) => o.kind);
    if (kinds.length === 0) return;
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    window.location.href = exportHref(portfolioId, kinds, timeZone);
    close();
  }

  return (
    <>
      <button
        type="button"
        onClick={(event) => {
          const button = event.currentTarget;
          setAnchor((current) => (current ? null : button));
        }}
        aria-haspopup="dialog"
        aria-expanded={anchor !== null}
        className="inline-flex items-center gap-2 rounded-lg bg-surface px-5 py-2.5 text-[0.9375rem] font-semibold transition-colors hover:bg-ink/10"
      >
        <DownloadIcon className="size-4" /> Exportar
      </button>
      {anchor && (
        <PopPanel anchor={anchor} label="Exportar" role="dialog" onClose={close} className="w-[22rem] max-w-[calc(100vw-1rem)] rounded-2xl p-2">
          <ul>
            {OPTIONS.map((option) => (
              <li key={option.kind}>
                <button
                  type="button"
                  role="checkbox"
                  aria-checked={selected.has(option.kind)}
                  onClick={() => toggle(option.kind)}
                  className="flex w-full cursor-pointer items-center justify-between gap-4 rounded-xl px-4 py-3 text-left transition-colors hover:bg-ink/10"
                >
                  <span>
                    <span className="block text-base font-bold">{option.title}</span>
                    {option.withDate && <span className="mt-0.5 block text-sm text-soft">A fecha de {dateFormat.format(new Date())}</span>}
                  </span>
                  <Checkbox checked={selected.has(option.kind)} />
                </button>
              </li>
            ))}
          </ul>
          <div className="mt-2 flex justify-end border-t border-rule px-2 pb-1 pt-3">
            <button
              type="button"
              onClick={download}
              disabled={selected.size === 0}
              className="rounded-lg bg-accent px-5 py-2.5 text-sm font-semibold text-on-accent transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
            >
              Exportar a CSV
            </button>
          </div>
        </PopPanel>
      )}
    </>
  );
}
