"use client";

import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { CurrencyNote } from "@/features/currency/components/CurrencyNote";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { PlusIcon } from "@/shared/components/Icons";
import { PctText } from "@/shared/components/PctText";
import { Hint, InfoHint } from "@/shared/components/Hint";
import { AllocationChart } from "@/features/portfolio/components/AllocationDonut";
import { AssetActionDialog, type AssetAction } from "@/features/portfolio/components/AssetDialogs";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { EmptyPortfolio } from "@/features/portfolio/components/EmptyPortfolio";
import { ExportMenu } from "@/features/portfolio/components/ExportMenu";
import { PerformanceChart } from "@/features/portfolio/components/PerformanceChart";
import { PortfolioAvatar } from "@/features/portfolio/components/PortfolioAvatar";
import { PortfolioDialog, type PortfolioDialogState } from "@/features/portfolio/components/PortfolioDialogs";
import { PortfolioMenu } from "@/features/portfolio/components/PortfolioMenu";
import { PortfolioChart } from "@/features/portfolio/components/PortfolioChart";
import { CARD, EyeButton, RANGE_BUTTONS, SENSITIVE, Segmented, useHideBalances } from "@/features/portfolio/components/PortfolioControls";
import { PortfolioSwitcher } from "@/features/portfolio/components/PortfolioSwitcher";
import type { AssetMenuChoice } from "@/features/portfolio/components/AssetMenu";
import { PositionsTable } from "@/features/portfolio/components/PositionsTable";
import { TransactionDialog, type DialogState } from "@/features/portfolio/components/TransactionDialog";
import { NO_FILTERS, TransactionFilters, type TransactionFilterState } from "@/features/portfolio/components/TransactionFilters";
import { TransactionHistory } from "@/features/portfolio/components/TransactionHistory";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { prefetchTopCoins } from "@/features/portfolio/lib/coinSearch";
import { buildAllocation } from "@/features/portfolio/lib/allocation";
import { directionOf } from "@/shared/lib/format";
import { bestAndWorst, type Performer } from "@/features/portfolio/lib/performers";
import { useFlags } from "@/features/portfolio/lib/portfolioFlags";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import { usePortfolioNav } from "@/features/portfolio/lib/useOpenPortfolio";
import type { PortfolioHistory, PortfolioInfo, PortfolioSummary, PortfolioValue, PositionDto, RangeCode } from "@/shared/lib/types";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 15_000;
const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;

type Tab = "activos" | "transacciones";
type ChartView = "historial" | "asignacion";

const CHART_VIEWS = [
  { code: "historial", text: "Historial" },
  { code: "asignacion", text: "Asignación" },
] as const;

/** Etiqueta «Predeterminado»: aparece y desaparece al instante al cambiar el interruptor del menú. */
function DefaultBadge({ portfolio }: { portfolio: PortfolioInfo }) {
  const { isDefault } = useFlags(portfolio);
  return isDefault ? <span className="rounded-full bg-accent px-3 py-0.5 text-xs font-bold text-on-accent">Predeterminado</span> : null;
}

/** Mejor o peor moneda. No es un enlace: el nombre completo aparece al pasar el ratón. */
function PerformerBlock({ title, performer }: { title: string; performer: Performer }) {
  const money = useMoney();
  return (
    <div className="min-w-0">
      <p className="text-sm text-soft">{title}</p>
      <Hint text={`${performer.name} (${performer.symbol})`} className="mt-1.5">
        <span tabIndex={0} className="flex items-center gap-2">
          <CoinIcon url={performer.imageUrl} size={26} />
          <span className="truncate text-lg font-bold">{performer.symbol}</span>
        </span>
      </Hint>
      <p className={`mt-1 text-sm font-semibold tabular-nums ${COLOR[directionOf(performer.pnlUsd)]}`}>
        <span className={SENSITIVE}>
          {money.signed(performer.pnlUsd)} <PctText value={performer.pnlPct} />
        </span>
      </p>
    </div>
  );
}

export function PortfolioView({
  initial,
  initialHistory,
  portfolios,
  portfolioId,
  values = null,
}: {
  initial: PortfolioSummary;
  initialHistory: PortfolioHistory | null;
  portfolios: PortfolioInfo[];
  portfolioId: string | null;
  values?: PortfolioValue[] | null;
}) {
  const money = useMoney();
  const router = useRouter();
  const { openCoin } = usePortfolioNav();
  const [version, setVersion] = useState(0);
  const [dialog, setDialog] = useState<DialogState | null>(null);
  const [tab, setTab] = useState<Tab>("activos");
  const [range, setRange] = useState<RangeCode>("1M");
  const [filters, setFilters] = useState<TransactionFilterState>(NO_FILTERS);
  const [hidden, toggleHidden] = useHideBalances();
  const [assetAction, setAssetAction] = useState<AssetAction | null>(null);
  const [chartView, setChartView] = useState<ChartView>("historial");
  const [portfolioDialog, setPortfolioDialog] = useState<PortfolioDialogState | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [panelMinHeight, setPanelMinHeight] = useState(0);

  // La lista de monedas de «Añadir transacción» se pide ya, para que el diálogo la muestre al abrirse.
  useEffect(() => {
    prefetchTopCoins().catch(() => undefined);
  }, []);

  /** Al cambiar de pestaña la nueva se queda al menos tan alta como la anterior: la página no se encoge ni salta. */
  function selectTab(next: Tab) {
    if (next === tab) return;
    setPanelMinHeight(panelRef.current?.offsetHeight ?? 0);
    setTab(next);
  }

  const { data, loading } = usePolledJson<PortfolioSummary>(`/api/portfolio?v=${version}${portfolioQuery(portfolioId)}`, initial, POLL_MS, {
    onUnauthorized: redirectToLogin,
  });

  const empty = data.positions.length === 0;
  // Sin ninguna transacción (ni siquiera vendidas): solo se invita a añadir la primera.
  const blank = empty && (portfolios.find((p) => p.id === portfolioId)?.transactionCount ?? 0) === 0;
  const { best, worst } = bestAndWorst(data.positions);
  const totalPnlPct = data.costBasisUsd > 0 ? (data.totalPnlUsd / data.costBasisUsd) * 100 : null;
  const historyIds = useMemo(() => [portfolioId], [portfolioId]);
  const current = portfolios.find((p) => p.id === portfolioId) ?? null;
  const slices = useMemo(() => buildAllocation(data.positions), [data.positions]);
  const filterCoins = useMemo(
    () => data.positions.map((p) => ({ slug: p.slug, symbol: p.symbol, imageUrl: p.imageUrl })),
    [data.positions],
  );

  function onMenu(p: PositionDto, choice: AssetMenuChoice) {
    if (choice === "transactions") void openCoin(p.slug); // la misma página que al pulsar la fila
    else setAssetAction({ kind: choice, position: p });
  }

  return (
    <div data-hide={hidden ? "true" : "false"} className="group grid gap-4 pt-1 lg:grid-cols-[17rem_minmax(0,1fr)] lg:gap-6">
      <aside className="lg:border-r lg:border-rule lg:pr-4">
        <PortfolioSwitcher portfolios={portfolios} selectedId={portfolioId} values={values} />
      </aside>

      <div className="min-w-0 space-y-3">
        <section aria-labelledby="resumen">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex items-center gap-4">
              {current && <PortfolioAvatar portfolio={current} size={60} />}
              <div>
                <h1 id="resumen" className="flex items-center gap-2 text-sm text-soft">
                  {data.name}
                  {current && <DefaultBadge portfolio={current} />}
                </h1>
                <div className="flex flex-wrap items-center gap-x-3">
                  <p className={`hero-num text-3xl font-bold tracking-tight tabular-nums sm:text-4xl ${SENSITIVE}`}>
                    {money.price(data.valueUsd)}
                  </p>
                  <span className="text-sm font-semibold">
                    <PctText value={data.change24hPct} />
                  </span>
                  <EyeButton hidden={hidden} onToggle={toggleHidden} />
                </div>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {!blank && (
                <>
                  <button
                    type="button"
                    onClick={() => setDialog({ mode: "new" })}
                    className="inline-flex items-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-on-accent hover:opacity-90"
                  >
                    <PlusIcon className="size-4" /> Añadir transacción
                  </button>
                  <ExportMenu portfolioId={portfolioId} />
                </>
              )}
              {current && (
                <PortfolioMenu
                  portfolio={current}
                  withSettings
                  align="start"
                  onAction={(kind) => setPortfolioDialog({ kind, portfolio: current })}
                  buttonClassName="inline-flex size-10 items-center justify-center rounded-lg bg-surface text-soft hover:text-ink"
                />
              )}
            </div>
          </div>

          <CurrencyNote />

          {data.unpricedPositions > 0 && (
            <p role="status" className="mt-3 rounded-xl bg-surface px-4 py-3 text-sm">
              {data.unpricedPositions === 1 ? "Una moneda no tiene precio ahora" : `${data.unpricedPositions} monedas no tienen precio ahora`}:
              no suma al valor total ni a la ganancia no realizada.
            </p>
          )}
          {data.inconsistentAssets > 0 && (
            <p role="alert" className="mt-3 rounded-xl border border-down px-4 py-3 text-sm text-down">
              {data.inconsistentAssets === 1 ? "El historial de una moneda no cuadra" : `El historial de ${data.inconsistentAssets} monedas no cuadra`}:
              se sacaron más unidades de las que había. Revisa sus transacciones.
            </p>
          )}
        </section>

        {blank ? (
          <EmptyPortfolio onAdd={() => setDialog({ mode: "new" })} />
        ) : (
          <>
        <div className="grid gap-3 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
          {data.portfolioId !== null && (
            <>
              <section aria-labelledby="inversiones" className={`${CARD} min-w-0`}>
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                  <h2 id="inversiones" className="text-xl font-bold">
                    Inversiones
                  </h2>
                  <div className="flex flex-wrap items-center gap-2">
                    {chartView === "historial" && <Segmented<RangeCode> label="Periodo del gráfico" value={range} onChange={setRange} options={RANGE_BUTTONS} />}
                    <Segmented<ChartView> label="Qué mostrar" value={chartView} onChange={setChartView} options={CHART_VIEWS} />
                  </div>
                </div>
                {chartView === "historial" ? (
                  <PortfolioChart ids={historyIds} initial={initialHistory} version={version} range={range} hidden={hidden} />
                ) : (
                  <AllocationChart slices={slices} loading={loading} />
                )}
              </section>

              <div className={`${CARD} min-w-0`}>
                <PerformanceChart portfolioId={portfolioId} version={version} hidden={hidden} />
              </div>
            </>
          )}

          <div className="grid gap-3 sm:grid-cols-2">
            <section aria-labelledby="beneficio" className={CARD}>
              <h2 id="beneficio" className="flex items-center gap-1.5 text-sm text-soft">
                Beneficio histórico
                <InfoHint>
                  <ul className="list-disc space-y-1 pl-4">
                    <li>Beneficio histórico = ganancia realizada + ganancia no realizada.</li>
                    <li>Ganancia realizada = (precio de venta × cantidad vendida − comisiones de venta) − coste de las unidades vendidas.</li>
                    <li>Ganancia no realizada = valor actual de lo que conservas − base de costo.</li>
                  </ul>
                </InfoHint>
              </h2>
              <p className={`mt-1 text-2xl font-bold tabular-nums ${COLOR[directionOf(data.totalPnlUsd)]} ${SENSITIVE}`}>{money.signed(data.totalPnlUsd)}</p>
              {totalPnlPct !== null && (
                <p className={`mt-1 text-sm font-semibold ${SENSITIVE}`}>
                  <PctText value={totalPnlPct} />
                </p>
              )}
            </section>

            <section aria-labelledby="base" className={CARD}>
              <h2 id="base" className="flex items-center gap-1.5 text-sm text-soft">
                Base de costo
                <InfoHint>
                  <p>Base de costo = suma de (precio de compra × cantidad comprada) + comisiones de compra, solo de las unidades que aún conservas.</p>
                  <p className="mt-1">Al vender se descuenta primero lo más antiguo (método FIFO).</p>
                </InfoHint>
              </h2>
              <p className={`mt-1 text-2xl font-bold tabular-nums ${SENSITIVE}`}>{money.price(data.costBasisUsd)}</p>
            </section>
          </div>

          <section aria-label="Mejor y peor rendimiento" className={`${CARD} flex items-center`}>
            {best && worst ? (
              <div className="grid w-full grid-cols-2 gap-4">
                <PerformerBlock title="Mejor rendimiento" performer={best} />
                <PerformerBlock title="Peor rendimiento" performer={worst} />
              </div>
            ) : (
              <p className="text-sm text-soft">Aparecen cuando haya monedas con precio.</p>
            )}
          </section>
        </div>

        <section aria-label="Activos y transacciones" className={`${CARD} px-0 pb-1`}>
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2 px-3">
            <div role="tablist" aria-label="Contenido del portafolio" className="flex gap-1">
              {(
                [
                  { code: "activos", text: "Activos" },
                  { code: "transacciones", text: "Transacciones" },
                ] as const
              ).map((t) => (
                <button
                  key={t.code}
                  type="button"
                  role="tab"
                  id={`tab-${t.code}`}
                  aria-selected={tab === t.code}
                  aria-controls={`panel-${t.code}`}
                  onClick={() => selectTab(t.code)}
                  className={`cursor-pointer rounded-lg px-4 py-2 text-sm font-semibold transition-colors ${tab === t.code ? "bg-accent/15 text-accent" : "text-soft hover:bg-ink/10 hover:text-ink"}`}
                >
                  {t.text}
                </button>
              ))}
            </div>
            {tab === "transacciones" && <TransactionFilters value={filters} onChange={setFilters} coins={filterCoins} />}
          </div>

          <div ref={panelRef} style={{ minHeight: panelMinHeight }}>
          {tab === "activos" ? (
            <div role="tabpanel" id="panel-activos" aria-labelledby="tab-activos">
              {empty ? (
                <div className="px-4 py-5">
                  <p className="font-medium">Todavía no tienes monedas en el portafolio.</p>
                  <button type="button" onClick={() => setDialog({ mode: "new" })} className="mt-3 rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-on-accent">
                    Añadir tu primera transacción
                  </button>
                </div>
              ) : (
                <PositionsTable
                  positions={data.positions}
                  portfolioId={portfolioId}
                  sensitiveClass={SENSITIVE}
                  onAdd={(p) => setDialog({ mode: "new", coin: { slug: p.slug, symbol: p.symbol, name: p.name, imageUrl: p.imageUrl } })}
                  onMenu={onMenu}
                />
              )}
            </div>
          ) : (
            <div role="tabpanel" id="panel-transacciones" aria-labelledby="tab-transacciones" className={`px-1 ${SENSITIVE}`}>
              <TransactionHistory
                portfolioId={portfolioId}
                version={version}
                slug={filters.coin === "all" ? null : filters.coin}
                type={filters.type === "all" ? null : filters.type}
                onEdit={(tx) => setDialog({ mode: "edit", tx })}
                onChanged={() => setVersion((v) => v + 1)}
              />
            </div>
          )}
          </div>
        </section>

          </>
        )}

        <p className="text-xs text-soft">Diario manual: no compra ni vende. Información orientativa, no es asesoramiento financiero.</p>
      </div>

      {assetAction && (
        <AssetActionDialog
          action={assetAction}
          portfolios={portfolios}
          values={values}
          portfolioId={portfolioId}
          onClose={() => setAssetAction(null)}
          onDone={() => {
            setAssetAction(null);
            setVersion((v) => v + 1);
            router.refresh(); // el menú lateral cuenta las transacciones de cada portafolio
          }}
        />
      )}

      {portfolioDialog && <PortfolioDialog state={portfolioDialog} onClose={() => setPortfolioDialog(null)} />}

      {dialog && (
        <TransactionDialog
          state={dialog}
          portfolioId={portfolioId}
          onClose={() => setDialog(null)}
          onSaved={() => {
            setDialog(null);
            setVersion((v) => v + 1);
            router.refresh();
          }}
        />
      )}
    </div>
  );
}
