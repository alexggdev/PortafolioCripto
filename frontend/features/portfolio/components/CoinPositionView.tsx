"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { ChevronLeftIcon, PlusIcon, TrashIcon } from "@/shared/components/Icons";
import { PctText } from "@/shared/components/PctText";
import { PopMenu } from "@/shared/components/PopMenu";
import { AssetActionDialog } from "@/features/portfolio/components/AssetDialogs";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { CARD as CARD_BASE, EyeButton, SENSITIVE, useHideBalances } from "@/features/portfolio/components/PortfolioControls";
import { PortfolioSwitcher } from "@/features/portfolio/components/PortfolioSwitcher";
import { TransactionDialog, type DialogState } from "@/features/portfolio/components/TransactionDialog";
import { TransactionHistory } from "@/features/portfolio/components/TransactionHistory";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { directionOf, formatQuantity } from "@/shared/lib/format";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import { usePortfolioNav } from "@/features/portfolio/lib/useOpenPortfolio";
import type { PortfolioInfo, PortfolioSummary, PortfolioValue } from "@/shared/lib/types";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 15_000;
const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;
const CARD = `${CARD_BASE} py-3`;

export interface CoinInfo {
  slug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
}

/** Una moneda dentro de un portafolio: cantidad, precio promedio de compra, ganancia y todas sus transacciones. */
export function CoinPositionView({
  coin,
  initial,
  portfolios,
  portfolioId,
  values,
}: {
  coin: CoinInfo;
  initial: PortfolioSummary;
  portfolios: PortfolioInfo[];
  portfolioId: string;
  values: PortfolioValue[] | null;
}) {
  const money = useMoney();
  const router = useRouter();
  const { backToPortfolio } = usePortfolioNav();
  const [version, setVersion] = useState(0);
  const [dialog, setDialog] = useState<DialogState | null>(null);
  const [removing, setRemoving] = useState(false);
  const [hidden, toggleHidden] = useHideBalances();

  const { data } = usePolledJson<PortfolioSummary>(`/api/portfolio?v=${version}${portfolioQuery(portfolioId)}`, initial, POLL_MS, {
    onUnauthorized: redirectToLogin,
  });
  const position = data.positions.find((p) => p.slug === coin.slug) ?? null;

  const quantity = position?.quantity ?? 0;
  const totalPnl = position ? (position.unrealizedPnlUsd ?? 0) + position.realizedPnlUsd : null;
  const totalPnlPct = position && position.costBasisUsd > 0 && totalPnl !== null ? (totalPnl / position.costBasisUsd) * 100 : null;

  return (
    <div data-hide={hidden ? "true" : "false"} className="group grid gap-4 pt-1 lg:grid-cols-[17rem_minmax(0,1fr)] lg:gap-6">
      <aside className="lg:border-r lg:border-rule lg:pr-4">
        <PortfolioSwitcher portfolios={portfolios} selectedId={portfolioId} values={values} />
      </aside>

      <div className="min-w-0 space-y-4">
        <button type="button" onClick={() => void backToPortfolio()} className="inline-flex cursor-pointer items-center gap-1 font-medium text-accent transition-opacity hover:opacity-80">
          <ChevronLeftIcon className="size-5" /> Atrás
        </button>

        <section aria-labelledby="moneda">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 id="moneda" className="flex items-center gap-2 text-lg text-soft">
                <CoinIcon url={coin.imageUrl} size={28} />
                <span>
                  {coin.name} ({coin.symbol.toUpperCase()})
                </span>
              </h1>
              <div className="mt-1 flex items-center gap-3">
                <p className={`hero-num text-3xl font-bold tracking-tight tabular-nums sm:text-4xl ${SENSITIVE}`}>
                  {money.price(position?.valueUsd ?? 0)}
                </p>
                <EyeButton hidden={hidden} onToggle={toggleHidden} />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setDialog({ mode: "new", coin, locked: true })}
                className="inline-flex items-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-on-accent hover:opacity-90"
              >
                <PlusIcon className="size-4" /> Agregar transacción
              </button>
              {position && (
                <PopMenu<"remove">
                  label={`Más acciones de ${coin.name}`}
                  items={[{ key: "remove", text: "Quitar del portafolio", danger: true, icon: <TrashIcon className="size-4" /> }]}
                  onChoose={() => setRemoving(true)}
                  buttonClassName="inline-flex size-10 items-center justify-center rounded-lg bg-surface text-soft hover:text-ink"
                />
              )}
            </div>
          </div>
        </section>

        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-[repeat(3,minmax(0,15rem))]">
          <div className={CARD}>
            <p className="text-sm text-soft">Cantidad</p>
            <p className={`mt-0.5 text-xl font-bold tabular-nums ${SENSITIVE}`}>
              {formatQuantity(quantity)} {coin.symbol.toUpperCase()}
            </p>
          </div>
          <div className={CARD}>
            <p className="text-sm text-soft">Precio promedio de compra</p>
            <p className={`mt-0.5 text-xl font-bold tabular-nums ${SENSITIVE}`}>{position ? money.price(position.averageCostUsd) : "--"}</p>
          </div>
          <div className={CARD}>
            <p className="text-sm text-soft">Ganancia / Pérdida Total</p>
            <p className={`mt-0.5 text-xl font-bold tabular-nums ${totalPnl === null ? "" : COLOR[directionOf(totalPnl)]} ${SENSITIVE}`}>
              {totalPnl === null ? "--" : money.signed(totalPnl)}
            </p>
            {totalPnlPct !== null && (
              <p className={`text-sm font-semibold ${SENSITIVE}`}>
                <PctText value={totalPnlPct} />
              </p>
            )}
          </div>
        </div>

        <section aria-labelledby="transacciones">
          <h2 id="transacciones" className="mb-2 text-xl font-bold">
            Transacciones
          </h2>
          <div className={SENSITIVE}>
            <TransactionHistory
              portfolioId={portfolioId}
              version={version}
              slug={coin.slug}
              showCoin={false}
              pageSize={100}
              onEdit={(tx) => setDialog({ mode: "edit", tx })}
              onChanged={() => {
                setVersion((v) => v + 1);
                router.refresh();
              }}
            />
          </div>
        </section>
      </div>

      {removing && position && (
        <AssetActionDialog
          action={{ kind: "remove", position }}
          portfolios={portfolios}
          values={values}
          portfolioId={portfolioId}
          onClose={() => setRemoving(false)}
          onDone={() => {
            setRemoving(false);
            void backToPortfolio(); // la moneda ya no está aquí
          }}
        />
      )}

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
