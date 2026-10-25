"use client";

import { useMemo, useState } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { CurrencyNote } from "@/features/currency/components/CurrencyNote";
import { GridIcon, PlusIcon } from "@/shared/components/Icons";
import { PctText } from "@/shared/components/PctText";
import { AllocationCard } from "@/features/portfolio/components/AllocationDonut";
import { OverviewTable } from "@/features/portfolio/components/OverviewTable";
import { PortfolioDialog } from "@/features/portfolio/components/PortfolioDialogs";
import { PortfolioChart } from "@/features/portfolio/components/PortfolioChart";
import { CARD, EyeButton, RANGE_BUTTONS, SENSITIVE, Segmented, useHideBalances } from "@/features/portfolio/components/PortfolioControls";
import { PortfolioSwitcher } from "@/features/portfolio/components/PortfolioSwitcher";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import type { PortfolioInfo, PortfolioOverview, RangeCode } from "@/shared/lib/types";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 15_000;

/** Todos los portafolios juntos: valor total, historial sumado, asignación y las monedas de todos. Solo lectura. */
export function OverviewView({ initial, portfolios }: { initial: PortfolioOverview; portfolios: PortfolioInfo[] }) {
  const money = useMoney();
  const [hidden, toggleHidden] = useHideBalances();
  const [range, setRange] = useState<RangeCode>("1M");
  const [creating, setCreating] = useState(false);

  const { data } = usePolledJson<PortfolioOverview>("/api/portfolio/overview", initial, POLL_MS, {
    onUnauthorized: redirectToLogin,
  });

  // Solo cuentan los portafolios marcados como «Contabilizar». El historial se pide por portafolio y se suma:
  // la lista de ids solo cambia si cambian los portafolios.
  const counted = useMemo(() => data.portfolios.filter((p) => p.includeInTotal), [data.portfolios]);
  const idsKey = counted.map((p) => p.id).join(",");
  const ids = useMemo(() => (idsKey ? idsKey.split(",") : []), [idsKey]);

  return (
    <div data-hide={hidden ? "true" : "false"} className="group grid gap-4 pt-1 lg:grid-cols-[17rem_minmax(0,1fr)] lg:gap-6">
      <aside className="lg:border-r lg:border-rule lg:pr-4">
        <PortfolioSwitcher portfolios={portfolios} selectedId={null} values={data.portfolios} overviewActive />
      </aside>

      <div className="min-w-0 space-y-3">
        <section aria-labelledby="total" className="flex items-center gap-4">
          <span aria-hidden="true" className="inline-flex size-12 shrink-0 items-center justify-center rounded-xl bg-accent text-on-accent">
            <GridIcon className="size-6" />
          </span>
          <div>
            <h1 id="total" className="text-sm text-soft">
              Valor total
            </h1>
            <div className="flex flex-wrap items-center gap-x-3">
              <p className={`hero-num text-3xl font-bold tracking-tight tabular-nums sm:text-4xl ${SENSITIVE}`}>{money.price(data.valueUsd)}</p>
              <span className="text-sm font-semibold">
                <PctText value={data.change24hPct} />
              </span>
              <EyeButton hidden={hidden} onToggle={toggleHidden} />
            </div>
          </div>
          <button
            type="button"
            onClick={() => setCreating(true)}
            className="ml-auto inline-flex items-center gap-2 self-start rounded-lg border border-accent px-4 py-2 text-sm font-semibold text-accent hover:bg-accent/10"
          >
            <PlusIcon className="size-4" /> Crear portafolio
          </button>
        </section>
        <CurrencyNote />

        {data.unpricedPositions > 0 && (
          <p role="status" className="rounded-xl bg-surface px-4 py-3 text-sm">
            {data.unpricedPositions === 1 ? "Una moneda no tiene precio ahora" : `${data.unpricedPositions} monedas no tienen precio ahora`}: no suma al valor total.
          </p>
        )}

        {data.portfolios.length === 0 ? (
          <div className={CARD}>
            <p className="text-soft">Aún no tienes portafolios.</p>
            <button type="button" onClick={() => setCreating(true)} className="mt-3 rounded-lg bg-accent px-4 py-2.5 font-semibold text-on-accent">
              Crear el primero
            </button>
          </div>
        ) : (
          <>
            <div className="grid gap-3 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
              <section aria-labelledby="historial" className={`${CARD} min-w-0`}>
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                  <h2 id="historial" className="text-xl font-bold">
                    Historial
                  </h2>
                  <Segmented<RangeCode> label="Periodo del gráfico" value={range} onChange={setRange} options={RANGE_BUTTONS} />
                </div>
                <PortfolioChart ids={ids} version={0} range={range} hidden={hidden} />
              </section>

              <div className={`${CARD} min-w-0`}>
                <AllocationCard positions={data.positions} portfolios={counted} />
              </div>
            </div>

            <section aria-labelledby="inversiones" className={`${CARD} min-w-0 px-0`}>
              <h2 id="inversiones" className="mb-2 px-4 text-xl font-bold">
                Inversiones
              </h2>
              <OverviewTable positions={data.positions} holders={data.holders ?? {}} portfolios={portfolios} sensitiveClass={SENSITIVE} />
            </section>
          </>
        )}
      </div>
      {creating && <PortfolioDialog state={{ kind: "create" }} onClose={() => setCreating(false)} />}
    </div>
  );
}
