"use client";

import { useEffect, useId, useMemo, useState, type KeyboardEvent, type PointerEvent } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { ChartTooltip, Dot, HIDDEN_VALUE, RecalculatingOverlay } from "@/features/portfolio/components/ChartParts";
import {
  compactAxisLabels,
  formatShortDate,
  formatTickDate,
  linePath,
  nearestIndex,
  niceTicks,
  pickDateTicks,
  scaleLinear,
} from "@/shared/lib/chart";
import { profitTone, toHistorySeries } from "@/features/portfolio/lib/portfolioHistory";
import type { PortfolioHistory, RangeCode } from "@/shared/lib/types";
import { useElementWidth } from "@/shared/lib/useElementWidth";
import { usePortfolioHistory } from "@/features/portfolio/lib/usePortfolioHistory";

const HEIGHT = 240;
const MARGIN = { top: 10, bottom: 26, left: 4 };
const AXIS_CHAR_PX = 7;

// Clases escritas completas para que Tailwind las encuentre.
const TONE = {
  up: { line: "stroke-up", dot: "bg-up", cssVar: "var(--up)" },
  down: { line: "stroke-down", dot: "bg-down", cssVar: "var(--down)" },
} as const;

/** Rango vertical ajustado a la línea (no parte de 0): así se ve la variación del periodo. */
function lineDomain(values: readonly number[]): [number, number] {
  const min = Math.min(...values);
  const max = Math.max(...values);
  const pad = (max - min) * 0.12 || Math.abs(max) * 0.02 || 1;
  return [min - pad, max + pad];
}

/**
 * Valor del portafolio en el tiempo. Verde si en el periodo hubo ganancia, rojo si hubo pérdida. Al pasar el ratón
 * muestra el valor y lo aportado ese día. Con `hidden` los importes no se escriben (la línea sí se ve).
 * Con varios `ids` suma esos portafolios (vista general).
 */
export function PortfolioChart({
  ids,
  initial = null,
  version,
  range,
  hidden,
}: {
  ids: readonly (string | null)[];
  initial?: PortfolioHistory | null;
  version: number;
  range: RangeCode;
  hidden: boolean;
}) {
  const money = useMoney();
  const gradientId = `grad-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const { data, loading, failing, key } = usePortfolioHistory(ids, range, version, initial);
  const [wrapRef, width] = useElementWidth();
  const [active, setActive] = useState<number | null>(null);
  useEffect(() => setActive(null), [key]);

  const series = useMemo(() => toHistorySeries(data?.points ?? [], money.rate), [data, money.rate]);
  const last = series.times.length - 1;
  const tone = TONE[profitTone(data?.periodProfitUsd)];

  const geometry = useMemo(() => {
    if (series.times.length < 2) return null;
    const [lo, hi] = lineDomain(series.value);
    const ticks = niceTicks(lo, hi, 4);
    const labels = compactAxisLabels(ticks.values, money.currency);
    const right = hidden ? 12 : Math.max(...labels.map((l) => l.length)) * AXIS_CHAR_PX + 14;
    const innerW = width - MARGIN.left - right;
    const baseline = HEIGHT - MARGIN.bottom;
    const t0 = series.times[0];
    const t1 = series.times[last];
    const x = scaleLinear([t0, t1], [MARGIN.left, MARGIN.left + innerW]);
    const y = scaleLinear([lo, hi], [baseline, MARGIN.top]);
    return {
      ticks,
      labels,
      right,
      innerW,
      baseline,
      t0,
      t1,
      x,
      xs: series.times.map((t) => x(t)),
      ys: series.value.map((v) => y(v)),
      dateTicks: pickDateTicks(t0, t1, width < 520 ? 3 : 5),
      y,
    };
  }, [series, last, width, hidden, money.currency]);

  function onPointerMove(event: PointerEvent<SVGSVGElement>) {
    if (!geometry) return;
    setActive(nearestIndex(geometry.xs, event.clientX - event.currentTarget.getBoundingClientRect().left));
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (last < 0) return;
    const current = active ?? last;
    const next =
      event.key === "ArrowLeft" ? Math.max(0, current - 1)
      : event.key === "ArrowRight" ? Math.min(last, current + 1)
      : event.key === "Home" ? 0
      : event.key === "End" ? last
      : null;
    if (event.key === "Escape") setActive(null);
    if (next !== null) {
      event.preventDefault();
      setActive(next);
    }
  }

  const shown = (v: number) => (hidden ? HIDDEN_VALUE : money.fmt(v));

  return (
    <div ref={wrapRef} className="relative min-h-[15rem]">
      {loading && <RecalculatingOverlay />}

      <div className={`transition duration-300 ${loading ? "pointer-events-none opacity-50 blur-sm" : ""}`}>
        {!geometry ? (
          <p role={failing ? "alert" : undefined} className="flex h-[15rem] items-center justify-center px-4 text-center text-soft">
            {failing && !data
              ? "No se pudo cargar el gráfico. Se reintentará en un momento."
              : data && data.points.length === 0
                ? "El gráfico aparece cuando añadas tu primera transacción."
                : data
                  ? "El gráfico se dibuja a partir del segundo día."
                  : ""}
          </p>
        ) : (
          <div
            tabIndex={0}
            onKeyDown={onKeyDown}
            onBlur={() => setActive(null)}
            role="group"
            aria-label={hidden ? "Gráfico del valor del portafolio con los importes ocultos" : `Valor de ${money.fmt(series.value[0])} a ${money.fmt(series.value[last])}. Usa las flechas para recorrer los días.`}
            className="relative"
          >
            <svg
              width={width}
              height={HEIGHT}
              aria-hidden="true"
              className="block touch-pan-y"
              onPointerMove={onPointerMove}
              onPointerDown={onPointerMove}
              onPointerLeave={() => setActive(null)}
            >
              <defs>
                <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0" style={{ stopColor: tone.cssVar, stopOpacity: 0.3 }} />
                  <stop offset="1" style={{ stopColor: tone.cssVar, stopOpacity: 0.02 }} />
                </linearGradient>
              </defs>

              {geometry.ticks.values.map((v, i) => (
                <g key={v}>
                  <line x1={MARGIN.left} x2={MARGIN.left + geometry.innerW} y1={geometry.y(v)} y2={geometry.y(v)} className="stroke-rule" />
                  {!hidden && (
                    <text x={MARGIN.left + geometry.innerW + 10} y={geometry.y(v)} dy="0.32em" className="fill-soft text-[11px]">
                      {geometry.labels[i]}
                    </text>
                  )}
                </g>
              ))}
              <line x1={MARGIN.left} x2={MARGIN.left + geometry.innerW} y1={geometry.baseline} y2={geometry.baseline} className="stroke-soft/50" />
              {geometry.dateTicks.map((t, i) => (
                <text
                  key={t}
                  x={geometry.x(t)}
                  y={HEIGHT - 6}
                  textAnchor={i === 0 ? "start" : i === geometry.dateTicks.length - 1 ? "end" : "middle"}
                  className="fill-soft text-[11px]"
                >
                  {formatTickDate(t, geometry.t1 - geometry.t0)}
                </text>
              ))}

              {/* `key` hace que la animación vuelva a empezar al cambiar de periodo, no en cada actualización. */}
              <g key={key}>
                <path
                  d={`${linePath(geometry.xs, geometry.ys)} L${geometry.xs[last].toFixed(1)},${geometry.baseline} L${geometry.xs[0].toFixed(1)},${geometry.baseline} Z`}
                  fill={`url(#${gradientId})`}
                  className="chart-area"
                />
                <path d={linePath(geometry.xs, geometry.ys)} pathLength={1} fill="none" strokeWidth={2.5} strokeLinejoin="round" strokeLinecap="round" className={`chart-line ${tone.line}`} />
              </g>

              {active !== null && (
                <g>
                  <line x1={geometry.xs[active]} x2={geometry.xs[active]} y1={MARGIN.top} y2={geometry.baseline} className="stroke-soft" strokeDasharray="4 4" />
                  <line x1={MARGIN.left} x2={MARGIN.left + geometry.innerW} y1={geometry.ys[active]} y2={geometry.ys[active]} className="stroke-soft" strokeDasharray="4 4" />
                  <circle cx={geometry.xs[active]} cy={geometry.ys[active]} r={4.5} className={`${tone.line} fill-surface`} strokeWidth={2.5} />
                </g>
              )}
            </svg>

            {active !== null && (
              <ChartTooltip x={geometry.xs[active]} width={width}>
                <p className="font-semibold">{formatShortDate(series.times[active])}</p>
                <p className="mt-2 flex items-center gap-2">
                  <Dot className={tone.dot} />
                  <span className="text-soft">Valor total:</span>
                  <span className="font-semibold tabular-nums">{shown(series.value[active])}</span>
                </p>
                <p className="mt-1.5 flex items-center gap-2">
                  <Dot className="bg-soft" />
                  <span className="text-soft">Aportado:</span>
                  <span className="tabular-nums">{shown(series.contributed[active])}</span>
                </p>
              </ChartTooltip>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
