"use client";

import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { PctText } from "@/shared/components/PctText";
import {
  areaPath,
  formatAxisNumber,
  formatLongDate,
  formatTickDate,
  linePath,
  nearestIndex,
  niceTicks,
  paddedDomain,
  pickDateTicks,
  scaleLinear,
  type SeriesPoint,
} from "@/shared/lib/chart";
import { directionOf } from "@/shared/lib/format";
import { DEFAULT_RANGE, RANGES } from "@/features/market/lib/ranges";
import type { AssetChart, RangeCode } from "@/shared/lib/types";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 30_000;
const MARGIN = { top: 12, right: 64, bottom: 28, left: 8 };
// Ancho aproximado de un carácter del eje Y (11 px): sirve para dejar sitio a cifras largas como 80.000.000.
const AXIS_CHAR_PX = 6.6;

const TONE = {
  up: { stroke: "stroke-up", fill: "fill-up" },
  down: { stroke: "stroke-down", fill: "fill-down" },
  flat: { stroke: "stroke-soft", fill: "fill-soft" },
} as const;

export function PriceChart({ slug, initial }: { slug: string; initial: AssetChart }) {
  const money = useMoney();
  const [range, setRange] = useState<RangeCode>(initial.range ?? DEFAULT_RANGE);
  const url = `/api/assets/${encodeURIComponent(slug)}/chart?range=${range}`;
  const { data: polled, loading } = usePolledJson<AssetChart>(url, initial, POLL_MS);
  // Mientras llega el rango nuevo, `polled` todavía es el anterior: se atenúa en vez de mostrarlo como si fuera el nuevo.
  const chart = polled;
  const option = RANGES.find((r) => r.code === range) ?? RANGES[1];

  const series: SeriesPoint[] = useMemo(
    () => chart.points.map((p) => ({ t: Date.parse(`${p.date}T00:00:00Z`), v: p.priceUsd * money.rate })),
    [chart.points, money.rate],
  );

  const wrapRef = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(640);
  useEffect(() => {
    const el = wrapRef.current;
    if (!el) return;
    const update = () => setWidth(Math.max(240, Math.floor(el.clientWidth)));
    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const height = width < 520 ? 240 : 320;
  const [active, setActive] = useState<number | null>(null);
  useEffect(() => setActive(null), [chart.points]);

  const geometry = useMemo(() => {
    if (series.length < 2) return null;
    const [lo, hi] = paddedDomain(series.map((s) => s.v));
    const yTicks = niceTicks(lo, hi, height < 300 ? 4 : 5);
    const widest = Math.max(1, ...yTicks.values.map((v) => formatAxisNumber(v, yTicks.step).length));
    const right = Math.max(MARGIN.right, Math.ceil(widest * AXIS_CHAR_PX) + 16);
    const innerW = width - MARGIN.left - right;
    const innerH = height - MARGIN.top - MARGIN.bottom;
    const t0 = series[0].t;
    const t1 = series[series.length - 1].t;
    const x = scaleLinear([t0, t1], [MARGIN.left, MARGIN.left + innerW]);
    const y = scaleLinear([lo, hi], [MARGIN.top + innerH, MARGIN.top]);
    const xs = series.map((s) => x(s.t));
    const ys = series.map((s) => y(s.v));
    const xTicks = pickDateTicks(t0, t1, width < 520 ? 3 : 5);
    return { innerW, innerH, right, yTicks, t0, t1, x, y, xs, ys, xTicks, baseline: MARGIN.top + innerH };
  }, [series, width, height]);

  const tone = TONE[directionOf(chart.changePct)];
  const lastIndex = series.length - 1;
  const shown = active ?? lastIndex;

  function onPointerMove(event: PointerEvent<SVGSVGElement>) {
    if (!geometry) return;
    const rect = event.currentTarget.getBoundingClientRect();
    setActive(nearestIndex(geometry.xs, event.clientX - rect.left));
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (series.length === 0) return;
    const current = active ?? lastIndex;
    let next: number | null = null;
    if (event.key === "ArrowLeft") next = Math.max(0, current - 1);
    else if (event.key === "ArrowRight") next = Math.min(lastIndex, current + 1);
    else if (event.key === "Home") next = 0;
    else if (event.key === "End") next = lastIndex;
    else if (event.key === "Escape") {
      setActive(null);
      return;
    }
    if (next !== null) {
      event.preventDefault();
      setActive(next);
    }
  }

  const point = series[shown];
  const isToday = chart.includesCurrentPrice && shown === lastIndex;
  const tooltipDate = point ? (isToday ? "Hoy, precio actual" : formatLongDate(point.t)) : "";

  return (
    <section aria-label="Gráfico de precio" className="mt-10">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <p className="text-lg">
          <PctText value={chart.changePct} /> <span className="text-soft">{option.phrase}</span>
        </p>
        <div role="group" aria-label="Periodo del gráfico" className="flex border border-rule">
          {RANGES.map((r) => (
            <button
              key={r.code}
              type="button"
              aria-pressed={r.code === range}
              onClick={() => setRange(r.code)}
              className={`px-3 py-1.5 text-sm font-medium ${
                r.code === range ? "bg-accent text-on-accent" : "bg-surface text-soft hover:text-ink"
              }`}
            >
              {r.label}
            </button>
          ))}
        </div>
      </div>

      <div ref={wrapRef} className={`mt-4 bg-surface transition-opacity ${loading ? "opacity-50" : ""}`}>
        {series.length === 0 ? (
          <p className="px-4 py-16 text-soft">Todavía no hay histórico de precios para esta moneda.</p>
        ) : !geometry ? (
          <p className="px-4 py-16 text-soft">Aún no hay suficiente histórico para dibujar el gráfico.</p>
        ) : (
          <div
            tabIndex={0}
            onKeyDown={onKeyDown}
            onBlur={() => setActive(null)}
            className="flex flex-col"
            aria-label={`Precio de ${money.fmt(series[0].v)} a ${money.fmt(series[lastIndex].v)} entre ${formatLongDate(geometry.t0)} y ${formatLongDate(geometry.t1)}. Usa las flechas para recorrer los días.`}
            role="group"
          >
            <svg
              viewBox={`0 0 ${width} ${height}`}
              role="img"
              aria-hidden="true"
              // Ancho 100 % y alto proporcional: antes de medir (render del servidor) no desborda y luego coincide 1:1.
              style={{ width: "100%", height: "auto" }}
              className="block touch-pan-y"
              onPointerMove={onPointerMove}
              onPointerDown={onPointerMove}
              onPointerLeave={() => setActive(null)}
            >
              {geometry.yTicks.values.map((v) => (
                <g key={v}>
                  <line
                    x1={MARGIN.left}
                    x2={MARGIN.left + geometry.innerW}
                    y1={geometry.y(v)}
                    y2={geometry.y(v)}
                    className="stroke-rule"
                    strokeWidth={1}
                  />
                  <text
                    x={width - geometry.right + 8}
                    y={geometry.y(v)}
                    dy="0.32em"
                    className="fill-soft text-[11px]"
                  >
                    {formatAxisNumber(v, geometry.yTicks.step)}
                  </text>
                </g>
              ))}
              {geometry.xTicks.map((t, i) => {
                const px = geometry.x(t);
                const anchor = i === 0 ? "start" : i === geometry.xTicks.length - 1 ? "end" : "middle";
                return (
                  <g key={t}>
                    <line x1={px} x2={px} y1={MARGIN.top} y2={geometry.baseline} className="stroke-rule" strokeWidth={1} />
                    <text x={px} y={height - 8} textAnchor={anchor} className="fill-soft text-[11px]">
                      {formatTickDate(t, geometry.t1 - geometry.t0)}
                    </text>
                  </g>
                );
              })}

              <path d={areaPath(geometry.xs, geometry.ys, geometry.baseline)} className={tone.fill} fillOpacity={0.1} />
              <path
                d={linePath(geometry.xs, geometry.ys)}
                className={tone.stroke}
                fill="none"
                strokeWidth={2}
                strokeLinejoin="round"
                strokeLinecap="round"
              />

              {active !== null && (
                <line
                  x1={geometry.xs[active]}
                  x2={geometry.xs[active]}
                  y1={MARGIN.top}
                  y2={geometry.baseline}
                  className="stroke-ink"
                  strokeWidth={1}
                  strokeDasharray="3 3"
                />
              )}
              <circle
                cx={geometry.xs[shown]}
                cy={geometry.ys[shown]}
                r={4}
                className={`${tone.fill} stroke-surface`}
                strokeWidth={2}
              />
            </svg>

            <div
              aria-live="polite"
              className="order-first flex flex-wrap items-baseline gap-x-3 px-3 pb-1 pt-3 text-sm"
            >
              <span className="font-semibold tabular-nums">{money.fmt(point.v)}</span>
              <span className="text-soft">{tooltipDate}</span>
            </div>
          </div>
        )}
      </div>

      {series.length > 0 && (
        <p className="mt-2 text-xs text-soft">Cierre diario de cada día UTC, en {money.currency}.</p>
      )}

      {series.length > 0 && (
        <details className="mt-3 text-sm">
          <summary className="cursor-pointer text-soft hover:text-ink">Ver los datos en una tabla</summary>
          <TableView series={series} chart={chart} />
        </details>
      )}
    </section>
  );
}

function TableView({ series, chart }: { series: SeriesPoint[]; chart: AssetChart }) {
  const money = useMoney();
  const rows = useMemo(() => [...series].reverse(), [series]);
  return (
    <div className="mt-2 max-h-72 overflow-y-auto border border-rule bg-surface">
      <table className="w-full border-collapse">
        <caption className="sr-only">Precio de cierre diario en {money.currency}</caption>
        <thead className="sticky top-0 bg-surface text-left text-soft">
          <tr>
            <th scope="col" className="px-3 py-2 font-medium">Fecha</th>
            <th scope="col" className="px-3 py-2 text-right font-medium">Precio ({money.currency})</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={row.t} className="border-t border-rule">
              <td className="px-3 py-1.5">
                {i === 0 && chart.includesCurrentPrice ? "Hoy, precio actual" : formatLongDate(row.t)}
              </td>
              <td className="px-3 py-1.5 text-right tabular-nums">{money.fmt(row.v)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
