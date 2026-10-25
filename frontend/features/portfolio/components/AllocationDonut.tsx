"use client";

import { useEffect, useState } from "react";
import { RecalculatingOverlay } from "@/features/portfolio/components/ChartParts";
import { SENSITIVE, Segmented } from "@/features/portfolio/components/PortfolioControls";
import { buildAllocation, buildPortfolioAllocation, formatSlicePct, type AllocationSlice } from "@/features/portfolio/lib/allocation";
import type { PortfolioValue, PositionDto } from "@/shared/lib/types";

// Tonos bien separados: azul, verde, naranja, coral, turquesa, violeta, rosa. «Otras» va en gris.
const COLORS = ["#4a74ff", "#16c784", "#f7a642", "#ff6f61", "#1fb6d1", "#a06bff", "#e0508f"];
const RADIUS = 46;
const STROKE = 12;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

type Mode = "token" | "portfolio";
const MODES = [
  { code: "token", text: "Token" },
  { code: "portfolio", text: "Portafolio" },
] as const;

const colorOf = (slice: AllocationSlice, index: number) => (slice.key === "otras" ? "var(--ink-soft)" : COLORS[index % COLORS.length]);
const legendLabel = (slice: AllocationSlice) => slice.symbol || slice.label;
const nameOf = (slice: AllocationSlice) => (slice.symbol ? `${slice.label} (${slice.symbol})` : slice.label);

/** Anillo con una animación de entrada: cada porción crece desde cero. Se vuelve a montar al cambiar de modo. */
function Ring({ slices, hover, onHover }: { slices: AllocationSlice[]; hover: number | null; onHover: (index: number | null, x?: number, y?: number) => void }) {
  const [grown, setGrown] = useState(false);
  useEffect(() => {
    const id = requestAnimationFrame(() => setGrown(true));
    return () => cancelAnimationFrame(id);
  }, []);

  let offset = 0;
  return (
    <svg viewBox="0 0 120 120" role="img" aria-label="Reparto del portafolio" className="size-52 shrink-0">
      <g transform="rotate(-90 60 60)" fill="none">
        {slices.map((slice, index) => {
          const length = Math.max((slice.pct / 100) * CIRCUMFERENCE - (slices.length > 1 ? 0.8 : 0), 0);
          const circle = (
            <circle
              key={slice.key}
              cx="60"
              cy="60"
              r={RADIUS}
              stroke={colorOf(slice, index)}
              strokeWidth={hover === index ? STROKE + 2.5 : STROKE}
              strokeDasharray={grown ? `${length} ${CIRCUMFERENCE - length}` : `0 ${CIRCUMFERENCE}`}
              strokeDashoffset={-offset}
              opacity={hover === null || hover === index ? 1 : 0.55}
              style={{ transition: "stroke-dasharray 900ms cubic-bezier(.2,.7,.2,1), stroke-width 150ms, opacity 150ms" }}
              onPointerMove={(event) => onHover(index, event.clientX, event.clientY)}
              onPointerLeave={() => onHover(null)}
            />
          );
          offset += (slice.pct / 100) * CIRCUMFERENCE;
          return circle;
        })}
      </g>
    </svg>
  );
}

/** Anillo y leyenda. Al pasar el ratón por una porción (o por su fila), un globo dice cuál es. */
export function AllocationChart({ slices, loading = false }: { slices: AllocationSlice[]; loading?: boolean }) {
  const [hover, setHover] = useState<{ index: number; x: number; y: number } | null>(null);
  const tip = hover ? slices[hover.index] : null;

  return (
    <div className="relative">
      {loading && <RecalculatingOverlay />}
      <div className={`transition duration-300 ${loading ? "pointer-events-none opacity-50 blur-sm" : ""}`}>
        {slices.length === 0 ? (
          <p className="py-16 text-center text-soft">Aún no hay monedas con precio.</p>
        ) : (
          <div className="flex flex-col items-center justify-center gap-6 py-4 sm:flex-row sm:gap-12">
            <Ring
              key={slices.map((slice) => slice.key).join()}
              slices={slices}
              hover={hover?.index ?? null}
              onHover={(index, x, y) => setHover(index === null ? null : { index, x: x ?? 0, y: y ?? 0 })}
            />
            <ul className="w-full max-w-[16rem] space-y-2.5">
              {slices.map((slice, index) => (
                <li key={slice.key} onPointerEnter={() => setHover({ index, x: 0, y: 0 })} onPointerLeave={() => setHover(null)} className="flex items-center justify-between gap-4">
                  <span className="flex min-w-0 items-center gap-3">
                    <span aria-hidden="true" className="size-4 shrink-0 rounded-full" style={{ background: colorOf(slice, index) }} />
                    <span className="truncate font-medium">{legendLabel(slice)}</span>
                  </span>
                  <span className={`shrink-0 tabular-nums ${SENSITIVE}`}>{formatSlicePct(slice.pct)}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>

      {tip && hover && hover.x > 0 && (
        <div
          role="tooltip"
          className="pointer-events-none fixed z-50 rounded-lg bg-black/90 px-3 py-1.5 text-sm font-semibold text-white shadow-xl"
          style={{ left: hover.x + 12, top: hover.y - 40 }}
        >
          {nameOf(tip)}
        </div>
      )}
    </div>
  );
}

/** «Asignación» de la vista general: se puede repartir por moneda o por portafolio. */
export function AllocationCard({ positions, portfolios, loading = false }: { positions: readonly PositionDto[]; portfolios: readonly PortfolioValue[]; loading?: boolean }) {
  const [mode, setMode] = useState<Mode>("token");
  const slices = mode === "token" ? buildAllocation(positions) : buildPortfolioAllocation(portfolios);

  return (
    <section aria-labelledby="asignacion">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="asignacion" className="text-xl font-bold">
          Asignación
        </h2>
        <Segmented<Mode> label="Repartir por" value={mode} options={MODES} onChange={setMode} />
      </div>
      <AllocationChart slices={slices} loading={loading} />
    </section>
  );
}
