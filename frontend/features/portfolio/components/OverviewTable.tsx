"use client";

import { Fragment, useMemo, useState } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { ChevronDownIcon } from "@/shared/components/Icons";
import { Hint } from "@/shared/components/Hint";
import { PctText } from "@/shared/components/PctText";
import { CoinCell } from "@/features/portfolio/components/CoinCell";
import { PortfolioAvatar } from "@/features/portfolio/components/PortfolioAvatar";
import { Th } from "@/features/portfolio/components/PositionsTable";
import { formatQuantity, NO_VALUE } from "@/shared/lib/format";
import { holderLines } from "@/features/portfolio/lib/holders";
import { ROW_HOVER } from "@/shared/lib/ui";
import { nextVisibleSort, sortPositions, type PositionSortKey, type SortDir } from "@/features/portfolio/lib/sortPositions";
import type { HolderShare, PortfolioInfo, PositionDto } from "@/shared/lib/types";

const CELL = "px-3 py-3";
const PCT_CELL = `hidden md:table-cell ${CELL}`;

/**
 * «Inversiones» de la vista general: una fila por moneda con sus datos sumados. En «Asignación del portafolio» van
 * las imágenes de los portafolios que la tienen; si son varios, pulsar esa celda despliega el reparto de cada uno.
 * Pulsar la celda de la moneda la abre en otra pestaña; el resto de la fila no hace nada.
 */
export function OverviewTable({
  positions,
  holders,
  portfolios,
  sensitiveClass,
}: {
  positions: readonly PositionDto[];
  holders: Record<string, HolderShare[]>;
  portfolios: readonly PortfolioInfo[];
  sensitiveClass: string;
}) {
  const money = useMoney();
  const [sort, setSort] = useState<{ key: PositionSortKey; dir: SortDir }>({ key: "value", dir: "desc" });
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());
  const sorted = useMemo(() => sortPositions(positions, sort.key, sort.dir), [positions, sort]);
  const onSort = (key: PositionSortKey) => setSort((s) => nextVisibleSort(positions, s, key));
  const portfolioOf = (id: string) => portfolios.find((p) => p.id === id);
  const toggle = (slug: string) =>
    setOpen((current) => {
      const next = new Set(current);
      if (!next.delete(slug)) next.add(slug);
      return next;
    });

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[46rem] border-collapse text-left text-sm">
        <caption className="sr-only">Monedas de todos tus portafolios</caption>
        <thead>
          <tr className="border-y border-rule">
            <Th label="Activos" column="name" sort={sort} onSort={onSort} align="left" />
            <th scope="col" className="px-3 py-3 text-left font-semibold">
              Asignación del portafolio
            </th>
            <Th label="Precio" column="price" sort={sort} onSort={onSort} />
            <Th label="1h%" column="change1h" sort={sort} onSort={onSort} className="hidden md:table-cell" />
            <Th label="24h%" column="change24h" sort={sort} onSort={onSort} />
            <Th label="7d%" column="change7d" sort={sort} onSort={onSort} className="hidden md:table-cell" />
            <Th label="Balance" column="quantity" sort={sort} onSort={onSort} />
            <Th label="Valor" column="value" sort={sort} onSort={onSort} />
          </tr>
        </thead>
        <tbody>
          {sorted.map((p) => {
            const lines = holderLines(holders[p.slug], p.priceUsd);
            const expandable = lines.length > 1;
            const expanded = expandable && open.has(p.slug);
            const align = expanded ? "align-top" : "";
            return (
              <Fragment key={p.assetId}>
                <tr className={`border-rule ${ROW_HOVER} ${expanded ? "" : "border-b"}`}>
                  <CoinCell slug={p.slug} name={p.name} symbol={p.symbol} imageUrl={p.imageUrl} />
                  <td className={`relative ${CELL} ${align}`}>
                    {expandable ? (
                      <button
                        type="button"
                        onClick={() => toggle(p.slug)}
                        aria-expanded={expanded}
                        aria-label={`Reparto de ${p.name} por portafolio`}
                        className="flex cursor-pointer items-center gap-2 after:absolute after:inset-0"
                      >
                        {expanded ? (
                          <span className="font-medium">Total</span>
                        ) : (
                          <span className="flex items-center">
                            {lines.map((line) => (
                              <span key={line.portfolioId} className="-ml-2 rounded-full ring-2 ring-surface first:ml-0">
                                <HolderAvatar portfolio={portfolioOf(line.portfolioId)} hint />
                              </span>
                            ))}
                          </span>
                        )}
                        <ChevronDownIcon className={`size-4 transition-transform ${expanded ? "rotate-180" : ""}`} />
                      </button>
                    ) : (
                      lines[0] && <HolderAvatar portfolio={portfolioOf(lines[0].portfolioId)} hint />
                    )}
                  </td>
                  <td className={`${CELL} text-right tabular-nums ${align}`}>{money.price(p.priceUsd)}</td>
                  <td className={`${PCT_CELL} text-right ${align}`}>
                    <PctText value={p.change1hPct} />
                  </td>
                  <td className={`${CELL} text-right ${align}`}>
                    <PctText value={p.change24hPct} />
                  </td>
                  <td className={`${PCT_CELL} text-right ${align}`}>
                    <PctText value={p.change7dPct} />
                  </td>
                  <td className={`${CELL} text-right tabular-nums ${align} ${sensitiveClass}`}>{formatQuantity(p.quantity)}</td>
                  <td className={`${CELL} text-right font-semibold tabular-nums ${align} ${sensitiveClass}`}>{p.valueUsd === null ? NO_VALUE : money.price(p.valueUsd)}</td>
                </tr>
                {expanded &&
                  lines.map((line, index) => {
                    const portfolio = portfolioOf(line.portfolioId);
                    return (
                      <tr key={line.portfolioId} className={`fade-in border-rule ${ROW_HOVER} ${index === lines.length - 1 ? "border-b" : ""}`}>
                        <td />
                        <td className={CELL}>
                          <span className="flex items-center gap-3">
                            <HolderAvatar portfolio={portfolio} />
                            <span className="font-medium">{portfolio?.name ?? "Portafolio"}</span>
                          </span>
                        </td>
                        <td />
                        <td className="hidden md:table-cell" />
                        <td />
                        <td className="hidden md:table-cell" />
                        <td className={`${CELL} text-right tabular-nums ${sensitiveClass}`}>{formatQuantity(line.quantity)}</td>
                        <td className={`${CELL} text-right tabular-nums ${sensitiveClass}`}>{line.valueUsd === null ? NO_VALUE : money.price(line.valueUsd)}</td>
                      </tr>
                    );
                  })}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** Imagen redonda de un portafolio; con `hint`, al pasar el ratón dice cuál es. */
function HolderAvatar({ portfolio, hint = false }: { portfolio: PortfolioInfo | undefined; hint?: boolean }) {
  if (!portfolio) return null;
  const avatar = <PortfolioAvatar portfolio={portfolio} size={28} round />;
  return hint ? <Hint text={portfolio.name}>{avatar}</Hint> : avatar;
}
