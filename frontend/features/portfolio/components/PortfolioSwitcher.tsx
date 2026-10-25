"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type CSSProperties, type PointerEvent } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { CheckIcon, GridIcon, GripIcon, PencilIcon, PlusIcon } from "@/shared/components/Icons";
import { PortfolioAvatar } from "@/features/portfolio/components/PortfolioAvatar";
import { PortfolioDialog, type PortfolioDialogState } from "@/features/portfolio/components/PortfolioDialogs";
import { PortfolioMenu } from "@/features/portfolio/components/PortfolioMenu";
import { reorderPortfolios } from "@/features/portfolio/lib/portfolioApi";
import { usePortfolioNav } from "@/features/portfolio/lib/useOpenPortfolio";
import { dropIndex, moveItem, totalOfIncluded } from "@/features/portfolio/lib/portfolios";
import type { PortfolioInfo, PortfolioValue } from "@/shared/lib/types";

const SETTLE_MS = 220;

/** Estado de un arrastre: de qué fila sale, a cuál va, cuánto se ha movido y cuánto mide cada fila. */
interface Drag {
  id: string;
  from: number;
  to: number;
  startY: number;
  dy: number;
  step: number;
  settling: boolean;
}

const ROW = "flex shrink-0 items-center gap-3 rounded-xl px-3 py-2.5 transition-colors lg:shrink";
const BLUR = "group-data-[hide=true]:blur-md";

/** Menú lateral como el de CoinMarketCap: Vista general, Mis portafolios (con lápiz para ordenarlos) y «+ Crear portafolio». */
export function PortfolioSwitcher({
  portfolios,
  selectedId,
  values,
  overviewActive = false,
}: {
  portfolios: PortfolioInfo[];
  selectedId: string | null;
  /** Valor actual de cada portafolio (opcional: si la API de resumen falla, no se muestra). */
  values?: PortfolioValue[] | null;
  /** True en la «Vista general»: marca ese enlace en vez de un portafolio. */
  overviewActive?: boolean;
}) {
  const money = useMoney();
  const router = useRouter();
  const { openPortfolio, openOverview } = usePortfolioNav();
  const [editing, setEditing] = useState(false);
  const [list, setList] = useState(portfolios);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [dropped, setDropped] = useState<string | null>(null);
  const [dialog, setDialog] = useState<PortfolioDialogState | null>(null);

  useEffect(() => setList(portfolios), [portfolios]);

  const valueOf = (id: string) => values?.find((v) => v.id === id)?.valueUsd ?? null;
  const total = values ? totalOfIncluded(values) : null;

  function startDrag(event: PointerEvent<HTMLElement>, id: string, index: number) {
    // Los eventos del menú «···» (que se dibuja fuera de la fila) también suben por React: se ignoran.
    if (event.button !== 0 || drag || !event.currentTarget.contains(event.target as Node) || (event.target as HTMLElement).closest("button")) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    const step = event.currentTarget.offsetHeight + 4; // la fila y el hueco de 4 px que la separa de la siguiente
    setDrag({ id, from: index, to: index, startY: event.clientY, dy: 0, step, settling: false });
  }

  function moveDrag(event: PointerEvent<HTMLElement>) {
    if (!drag || drag.settling) return;
    const dy = event.clientY - drag.startY;
    setDrag({ ...drag, dy, to: dropIndex(drag.from, dy, drag.step, list.length) });
  }

  /** Al soltar, la fila se desliza hasta su hueco y solo entonces se guarda el orden. */
  function endDrag() {
    if (!drag || drag.settling) return;
    const { id, from, to, step } = drag;
    setDrag({ ...drag, dy: (to - from) * step, settling: true });
    window.setTimeout(async () => {
      const next = moveItem(list, from, to);
      setList(next);
      setDrag(null);
      setDropped(id);
      window.setTimeout(() => setDropped(null), 700);
      if (from === to) return;
      const result = await reorderPortfolios(next.map((p) => p.id));
      if (result.ok) router.refresh();
      else setList(portfolios);
    }, SETTLE_MS);
  }

  /** Cómo se dibuja cada fila durante el arrastre: la levantada sigue al ratón y las demás se apartan. */
  function dragStyle(id: string, index: number): { className: string; style?: CSSProperties } {
    if (!drag) return { className: dropped === id ? "drop-flash" : "" };
    if (id === drag.id) {
      return {
        className: `relative z-30 scale-[1.03] bg-surface shadow-2xl ring-2 ring-accent ${drag.settling ? "" : "cursor-grabbing"}`,
        style: {
          transform: `translateY(${drag.dy}px)`,
          transition: drag.settling ? `transform ${SETTLE_MS}ms cubic-bezier(.2,.8,.2,1)` : "none",
        },
      };
    }
    const shift = drag.from < drag.to && index > drag.from && index <= drag.to ? -drag.step : drag.from > drag.to && index < drag.from && index >= drag.to ? drag.step : 0;
    return { className: "", style: { transform: `translateY(${shift}px)`, transition: `transform ${SETTLE_MS}ms cubic-bezier(.2,.8,.2,1)` } };
  }

  const subtitle = (p: PortfolioInfo) => {
    const value = valueOf(p.id);
    return value !== null ? money.price(value) : p.transactionCount === 1 ? "1 transacción" : `${p.transactionCount} transacciones`;
  };

  return (
    <div className="space-y-3">
      <Link href="/portafolio" prefetch={false} onClick={(event) => { event.preventDefault(); void openOverview(); }} aria-current={overviewActive ? "page" : undefined} className={`${ROW} ${overviewActive ? "bg-surface" : "hover:bg-surface"}`}>
        <span aria-hidden="true" className="inline-flex size-10 shrink-0 items-center justify-center rounded-xl bg-accent text-on-accent">
          <GridIcon className="size-5" />
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold">Vista general</span>
          <span className={`block truncate text-sm text-soft ${BLUR}`}>{total === null ? "Todos juntos" : money.price(total)}</span>
        </span>
      </Link>

      <div className="flex items-center justify-between px-1">
        <h2 className="text-base font-bold">Mis portafolios({portfolios.length})</h2>
        <button
          type="button"
          onClick={() => setEditing((e) => !e)}
          aria-pressed={editing}
          aria-label={editing ? "Terminar de ordenar" : "Ordenar y editar portafolios"}
          title={editing ? "Terminar" : "Ordenar y editar"}
          className="inline-flex size-8 items-center justify-center rounded-lg text-soft hover:bg-surface hover:text-ink"
        >
          {editing ? <CheckIcon /> : <PencilIcon />}
        </button>
      </div>

      {/* En pantallas pequeñas la lista es horizontal; en grandes, una columna. */}
      <nav aria-label="Tus portafolios" className={`flex gap-2 ${editing ? "flex-col gap-1" : "overflow-x-auto pb-1 lg:flex-col lg:gap-1 lg:overflow-visible lg:pb-0"}`}>
        {list.map((p, index) => {
          if (!editing) {
            const active = !overviewActive && p.id === selectedId;
            return (
              <Link key={p.id} href="/portafolio" prefetch={false} onClick={(event) => { event.preventDefault(); void openPortfolio(p.id); }} aria-current={active ? "page" : undefined} className={`${ROW} ${active ? "bg-surface" : "hover:bg-surface"}`}>
                <PortfolioAvatar portfolio={p} size={40} />
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold">{p.name}</span>
                  <span className={`block truncate text-sm text-soft ${BLUR}`}>{subtitle(p)}</span>
                </span>
              </Link>
            );
          }
          const { className, style } = dragStyle(p.id, index);
          return (
            <div
              key={p.id}
              onPointerDown={(event) => startDrag(event, p.id, index)}
              onPointerMove={moveDrag}
              onPointerUp={endDrag}
              onPointerCancel={endDrag}
              style={style}
              className={`${ROW} fade-in touch-none cursor-grab select-none bg-surface/70 transition-colors hover:bg-surface ${className}`}
            >
              <GripIcon className="size-4 shrink-0 text-soft" />
              <PortfolioAvatar portfolio={p} size={40} />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold">{p.name}</span>
                <span className={`block truncate text-sm text-soft ${BLUR}`}>{subtitle(p)}</span>
              </span>
              <PortfolioMenu portfolio={p} align="start" onAction={(kind) => setDialog({ kind, portfolio: p })} />
            </div>
          );
        })}
      </nav>

      <button type="button" onClick={() => setDialog({ kind: "create" })} className="flex w-full items-center gap-2 rounded-xl px-3 py-2.5 text-sm font-medium text-accent hover:bg-surface">
        <PlusIcon /> Crear portafolio
      </button>
      {dialog && <PortfolioDialog state={dialog} onClose={() => setDialog(null)} />}
    </div>
  );
}
