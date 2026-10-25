"use client";

import { useMemo, useState } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { PlusIcon, SortIcon } from "@/shared/components/Icons";
import { PctText } from "@/shared/components/PctText";
import { AssetMenu, type AssetMenuChoice } from "@/features/portfolio/components/AssetMenu";
import { CoinCell } from "@/features/portfolio/components/CoinCell";
import { directionOf, formatQuantity, NO_VALUE } from "@/shared/lib/format";
import { usePortfolioNav } from "@/features/portfolio/lib/useOpenPortfolio";
import { ROW_HOVER } from "@/shared/lib/ui";
import { nextVisibleSort, sortPositions, type PositionSortKey, type SortDir } from "@/features/portfolio/lib/sortPositions";
import type { PositionDto } from "@/shared/lib/types";

const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;

export function Th({
  label,
  column,
  sort,
  onSort,
  className = "",
  align = "right",
}: {
  label: string;
  column: PositionSortKey;
  sort: { key: PositionSortKey; dir: SortDir };
  onSort: (key: PositionSortKey) => void;
  className?: string;
  align?: "left" | "right";
}) {
  const state = sort.key === column ? sort.dir : "none";
  return (
    <th
      scope="col"
      aria-sort={state === "none" ? "none" : state === "asc" ? "ascending" : "descending"}
      className={`px-3 py-3 ${align === "right" ? "text-right" : "text-left"} ${className}`}
    >
      <button type="button" onClick={() => onSort(column)} className="inline-flex items-center whitespace-nowrap cursor-pointer font-semibold text-ink">
        {label}
        <SortIcon state={state} />
      </button>
    </th>
  );
}

/**
 * Tabla de activos como la de CoinMarketCap: Nombre · Precio · 1h% · 24h% · 7d% · Inversiones (valor y cantidad) ·
 * Precio promedio de compra · Ganancia/Pérdida (importe y %) · Acciones (+ y ···). Cada cabecera ordena.
 */
export function PositionsTable({
  positions,
  portfolioId,
  sensitiveClass,
  onAdd,
  onMenu,
}: {
  positions: readonly PositionDto[];
  portfolioId: string | null;
  sensitiveClass: string;
  onAdd: (p: PositionDto) => void;
  onMenu: (p: PositionDto, choice: AssetMenuChoice) => void;
}) {
  const money = useMoney();
  const { openCoin } = usePortfolioNav();
  const [sort, setSort] = useState<{ key: PositionSortKey; dir: SortDir }>({ key: "value", dir: "desc" });
  const sorted = useMemo(() => sortPositions(positions, sort.key, sort.dir), [positions, sort]);
  const onSort = (key: PositionSortKey) => setSort((s) => nextVisibleSort(positions, s, key));

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[46rem] border-collapse text-left text-sm">
        <caption className="sr-only">Activos de tu portafolio</caption>
        <thead>
          <tr className="border-y border-rule">
            <Th label="Nombre" column="name" sort={sort} onSort={onSort} align="left" />
            <Th label="Precio" column="price" sort={sort} onSort={onSort} />
            <Th label="1h%" column="change1h" sort={sort} onSort={onSort} className="hidden md:table-cell" />
            <Th label="24h%" column="change24h" sort={sort} onSort={onSort} />
            <Th label="7d%" column="change7d" sort={sort} onSort={onSort} className="hidden md:table-cell" />
            <Th label="Inversiones" column="value" sort={sort} onSort={onSort} />
            <Th label="Precio promedio de compra" column="avgCost" sort={sort} onSort={onSort} className="hidden xl:table-cell" />
            <Th label="Ganancia/Pérdida" column="pnl" sort={sort} onSort={onSort} />
            <th scope="col" className="px-3 py-3 text-right font-semibold">
              Acciones
            </th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((p) => (
            <tr
              key={p.assetId}
              onClick={() => void openCoin(p.slug)}
              className={`cursor-pointer border-b border-rule ${ROW_HOVER}`}
            >
              <CoinCell slug={p.slug} name={p.name} symbol={p.symbol} imageUrl={p.imageUrl} />
              <td className="px-3 py-3 text-right tabular-nums">{money.price(p.priceUsd)}</td>
              <td className="hidden px-3 py-3 text-right md:table-cell">
                <PctText value={p.change1hPct} />
              </td>
              <td className="px-3 py-3 text-right">
                <PctText value={p.change24hPct} />
              </td>
              <td className="hidden px-3 py-3 text-right md:table-cell">
                <PctText value={p.change7dPct} />
              </td>
              <td className="px-3 py-3 text-right tabular-nums">
                <p className={`font-semibold ${sensitiveClass}`}>{p.valueUsd === null ? NO_VALUE : money.price(p.valueUsd)}</p>
                <p className={`text-xs text-soft ${sensitiveClass}`}>
                  {formatQuantity(p.quantity)} {p.symbol.toUpperCase()}
                </p>
              </td>
              <td className={`hidden px-3 py-3 text-right tabular-nums xl:table-cell ${sensitiveClass}`}>{money.price(p.averageCostUsd)}</td>
              <td className="px-3 py-3 text-right tabular-nums">
                <p className={`font-semibold ${COLOR[directionOf(p.unrealizedPnlUsd)]} ${sensitiveClass}`}>
                  {p.unrealizedPnlUsd === null ? NO_VALUE : money.signed(p.unrealizedPnlUsd)}
                </p>
                <p className={`text-xs ${sensitiveClass}`}>
                  <PctText value={p.unrealizedPnlPct} />
                </p>
              </td>
              <td className="px-3 py-3 text-right">
                <span className="inline-flex items-center gap-1" onClick={(event) => event.stopPropagation()}>
                  <button
                    type="button"
                    onClick={() => onAdd(p)}
                    aria-label={`Añadir transacción de ${p.name}`}
                    title="Añadir transacción"
                    className="inline-flex size-8 items-center justify-center rounded-lg text-soft hover:bg-surface hover:text-ink"
                  >
                    <PlusIcon />
                  </button>
                  <AssetMenu label={p.name} onChoose={(choice) => onMenu(p, choice)} />
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
