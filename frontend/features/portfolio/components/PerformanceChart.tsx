"use client";

import { useEffect, useMemo, useState, type PointerEvent } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { InfoHint } from "@/shared/components/Hint";
import { PctText } from "@/shared/components/PctText";
import { ChartTooltip, Dot, HIDDEN_VALUE, RecalculatingOverlay } from "@/features/portfolio/components/ChartParts";
import { compareWithBtc } from "@/features/portfolio/lib/benchmark";
import { formatPctAxis, formatShortDate, formatTickDate, linePath, nearestIndex, niceTicks, pickDateTicks, scaleLinear } from "@/shared/lib/chart";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { directionOf } from "@/shared/lib/format";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import type { AssetChart, PortfolioHistory } from "@/shared/lib/types";
import { useElementWidth } from "@/shared/lib/useElementWidth";

const HEIGHT = 250;
const MARGIN = { top: 10, right: 54, bottom: 26, left: 4 };
const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;

type State =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; history: PortfolioHistory; btc: AssetChart };

type SeriesKey = "profit" | "btc";

/** Interruptor de la leyenda: el círculo se apaga cuando la serie está oculta. */
function LegendToggle({ on, onToggle, color, children }: { on: boolean; onToggle: () => void; color: string; children: string }) {
  return (
    <button
      type="button"
      aria-pressed={on}
      onClick={onToggle}
      className={`inline-flex items-center gap-2 rounded-full py-1 text-sm font-medium transition-colors ${on ? "text-ink" : "text-soft hover:text-ink"}`}
    >
      <Dot className={on ? color : "bg-soft/40"} />
      {children}
    </button>
  );
}

/**
 * «Rendimiento»: ganancia total del portafolio frente a la tendencia de Bitcoin desde el primer día con saldo.
 * Cada serie se puede apagar desde la leyenda; el tooltip da la ganancia en dinero y en %, y el % de Bitcoin.
 */
export function PerformanceChart({ portfolioId, version, hidden }: { portfolioId: string | null; version: number; hidden: boolean }) {
  const money = useMoney();
  const [state, setState] = useState<State>({ status: "loading" });
  const [visible, setVisible] = useState<Record<SeriesKey, boolean>>({ profit: true, btc: true });
  const [active, setActive] = useState<number | null>(null);
  const [wrapRef, width] = useElementWidth();

  useEffect(() => {
    const controller = new AbortController();
    setState({ status: "loading" });
    (async () => {
      try {
        const [h, b] = await Promise.all([
          fetch(`/api/portfolio/history?range=All&v=${version}${portfolioQuery(portfolioId)}`, { cache: "no-store", signal: controller.signal }),
          fetch("/api/assets/bitcoin/chart?range=All", { cache: "no-store", signal: controller.signal }),
        ]);
        if (h.status === 401) {
          redirectToLogin();
          return;
        }
        if (!h.ok || !b.ok) throw new Error("http");
        setState({ status: "ready", history: (await h.json()) as PortfolioHistory, btc: (await b.json()) as AssetChart });
      } catch {
        if (!controller.signal.aborted) setState({ status: "error" });
      }
    })();
    return () => controller.abort();
  }, [version, portfolioId]);

  const comparison = useMemo(
    () => (state.status === "ready" ? compareWithBtc(state.history.points, state.btc.points) : null),
    [state],
  );

  const geometry = useMemo(() => {
    if (!comparison) return null;
    const shownValues = [0, ...(visible.profit ? comparison.portfolioPct : []), ...(visible.btc ? comparison.btcPct : [])];
    const low = Math.min(...shownValues);
    const high = Math.max(...shownValues);
    const pad = (high - low) * 0.08 || 1;
    const floor = low - pad;
    const ceiling = high + pad;
    const ticks = niceTicks(floor, ceiling, 5);
    const innerW = width - MARGIN.left - MARGIN.right;
    const baseline = HEIGHT - MARGIN.bottom;
    const t0 = comparison.times[0];
    const t1 = comparison.times[comparison.times.length - 1];
    const x = scaleLinear([t0, t1], [MARGIN.left, MARGIN.left + innerW]);
    const y = scaleLinear([floor, ceiling], [baseline, MARGIN.top]);
    return {
      ticks,
      innerW,
      baseline,
      t0,
      t1,
      x,
      y,
      xs: comparison.times.map((t) => x(t)),
      profitYs: comparison.portfolioPct.map((v) => y(v)),
      btcYs: comparison.btcPct.map((v) => y(v)),
      dateTicks: pickDateTicks(t0, t1, width < 520 ? 3 : 5),
    };
  }, [comparison, visible, width]);

  const toggle = (key: SeriesKey) => setVisible((v) => ({ ...v, [key]: !v[key] }));
  const last = comparison ? comparison.times.length - 1 : -1;
  const loading = state.status === "loading";

  function onPointerMove(event: PointerEvent<SVGSVGElement>) {
    if (geometry) setActive(nearestIndex(geometry.xs, event.clientX - event.currentTarget.getBoundingClientRect().left));
  }

  return (
    <section aria-labelledby="rendimiento">
      <h2 id="rendimiento" className="flex items-center gap-2 text-xl font-bold">
        Rendimiento
        <InfoHint below>
          <span className="mb-1 block font-semibold">Rendimiento (acumulado)</span>
          <ol className="list-decimal space-y-1 pl-4">
            <li>
              «Ganancia total» es la rentabilidad del portafolio sin contar lo que ingresas o retiras: cada día, (valor de hoy − dinero que entró hoy) ÷ valor
              de ayer. En el aviso se muestra también la ganancia en dinero: valor − aportado.
            </li>
            <li>«Tendencia BTC» es lo que ha variado Bitcoin (cierre diario) desde el primer día con saldo del portafolio.</li>
          </ol>
        </InfoHint>
      </h2>

      <div className="mt-2 flex flex-wrap gap-x-5">
        <LegendToggle on={visible.profit} onToggle={() => toggle("profit")} color="bg-accent">
          Ganancia total
        </LegendToggle>
        <LegendToggle on={visible.btc} onToggle={() => toggle("btc")} color="bg-btc">
          Tendencia BTC
        </LegendToggle>
      </div>

      <div ref={wrapRef} className="relative mt-2 min-h-[15rem]">
        {loading && <RecalculatingOverlay />}
        {state.status === "error" ? (
          <p role="alert" className="flex h-[15rem] items-center justify-center text-center text-soft">
            No se pudo cargar la comparación. Prueba de nuevo en un momento.
          </p>
        ) : loading ? (
          <div className="h-[15rem]" />
        ) : !geometry || !comparison ? (
          <p className="flex h-[15rem] items-center justify-center px-4 text-center text-soft">Hacen falta al menos dos días con saldo.</p>
        ) : (
          <div className="relative">
            <svg width={width} height={HEIGHT} role="img" aria-label="Ganancia total del portafolio frente a Bitcoin" className="block touch-pan-y" onPointerMove={onPointerMove} onPointerDown={onPointerMove} onPointerLeave={() => setActive(null)}>
              {geometry.ticks.values.map((v) => (
                <g key={v}>
                  <line x1={MARGIN.left} x2={MARGIN.left + geometry.innerW} y1={geometry.y(v)} y2={geometry.y(v)} className="stroke-rule" />
                  {!hidden && (
                    <text x={MARGIN.left + geometry.innerW + 10} y={geometry.y(v)} dy="0.32em" className="fill-soft text-[11px]">
                      {formatPctAxis(v)}
                    </text>
                  )}
                </g>
              ))}
              <line x1={MARGIN.left} x2={MARGIN.left + geometry.innerW} y1={geometry.y(0)} y2={geometry.y(0)} className="stroke-soft/50" />
              {geometry.dateTicks.map((t, i) => (
                <g key={t}>
                  <line x1={geometry.x(t)} x2={geometry.x(t)} y1={geometry.baseline} y2={geometry.baseline + 5} className="stroke-soft/50" />
                  <text x={geometry.x(t)} y={HEIGHT - 6} textAnchor={i === 0 ? "start" : i === geometry.dateTicks.length - 1 ? "end" : "middle"} className="fill-soft text-[11px]">
                    {formatTickDate(t, geometry.t1 - geometry.t0)}
                  </text>
                </g>
              ))}

              {visible.profit && (
                <g>
                  <path
                    d={`${linePath(geometry.xs, geometry.profitYs)} L${geometry.xs[last].toFixed(1)},${geometry.y(0).toFixed(1)} L${geometry.xs[0].toFixed(1)},${geometry.y(0).toFixed(1)} Z`}
                    className="chart-area fill-accent/15"
                  />
                  <path d={linePath(geometry.xs, geometry.profitYs)} pathLength={1} fill="none" strokeWidth={2.5} strokeLinejoin="round" className="chart-line stroke-accent" />
                </g>
              )}
              {visible.btc && (
                <path d={linePath(geometry.xs, geometry.btcYs)} pathLength={1} fill="none" strokeWidth={2.5} strokeLinejoin="round" className="chart-line stroke-btc" />
              )}

              {active !== null && (
                <g>
                  <line x1={geometry.xs[active]} x2={geometry.xs[active]} y1={MARGIN.top} y2={geometry.baseline} className="stroke-soft" strokeDasharray="4 4" />
                  {visible.profit && <circle cx={geometry.xs[active]} cy={geometry.profitYs[active]} r={4.5} className="fill-surface stroke-accent" strokeWidth={2.5} />}
                  {visible.btc && <circle cx={geometry.xs[active]} cy={geometry.btcYs[active]} r={4.5} className="fill-surface stroke-btc" strokeWidth={2.5} />}
                </g>
              )}
            </svg>

            {active !== null && (visible.profit || visible.btc) && (
              <ChartTooltip x={geometry.xs[active]} width={width}>
                <p className="font-semibold">{formatShortDate(comparison.times[active])}</p>
                {visible.profit && (
                  <p className="mt-2 flex items-center gap-2">
                    <Dot className="bg-accent" />
                    <span className="text-soft">Ganancia total:</span>
                    {hidden ? (
                      <span className="font-semibold">{HIDDEN_VALUE}</span>
                    ) : (
                      <>
                        <span className={`font-semibold tabular-nums ${COLOR[directionOf(comparison.profitUsd[active])]}`}>{money.signed(comparison.profitUsd[active])},</span>
                        <PctText value={comparison.portfolioPct[active]} />
                      </>
                    )}
                  </p>
                )}
                {visible.btc && (
                  <p className="mt-1.5 flex items-center gap-2">
                    <Dot className="bg-btc" />
                    <span className="text-soft">Tendencia BTC:</span>
                    {hidden ? <span className="font-semibold">{HIDDEN_VALUE}</span> : <PctText value={comparison.btcPct[active]} />}
                  </p>
                )}
              </ChartTooltip>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
