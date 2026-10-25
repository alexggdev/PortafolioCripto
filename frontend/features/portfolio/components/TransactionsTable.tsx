"use client";

import Link from "next/link";
import { useMemo, useState, type KeyboardEvent, type MouseEvent } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { PencilIcon, SortIcon, TrashIcon } from "@/shared/components/Icons";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { TypeBadge } from "@/features/portfolio/components/TypeBadge";
import { formatQuantity } from "@/shared/lib/format";
import { sortTransactions, txAmountUsd, txDirection } from "@/features/portfolio/lib/transactionRows";
import { typeLabel } from "@/features/portfolio/lib/transactions";
import { ROW_HOVER } from "@/shared/lib/ui";
import type { TransactionDto } from "@/shared/lib/types";

const dateFormat = new Intl.DateTimeFormat("es-ES", {
  day: "numeric",
  month: "short",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false,
});
const COLOR = { 1: "text-up", [-1]: "text-down" } as const;
const NONE = "--";
const ICON_BUTTON = "inline-flex size-9 items-center justify-center rounded-lg text-soft hover:bg-paper";

/**
 * Tabla de transacciones: Tipo · Fecha · Activos · Precio · Importe (cantidad y total) · Comisiones · Notas · Acciones.
 * Pulsar una fila abre su detalle; la zona de la moneda es un enlace a su ficha en otra pestaña.
 */
export function TransactionsTable({
  rows,
  showCoin,
  onOpen,
  onEdit,
  onDelete,
  loading = false,
}: {
  rows: readonly TransactionDto[];
  showCoin: boolean;
  /** Pulsar la fila (fuera de los enlaces y botones) abre el detalle de la transacción. */
  onOpen: (tx: TransactionDto) => void;
  onEdit: (tx: TransactionDto) => void;
  /** Pide borrar: quien la usa muestra el aviso de confirmación. */
  onDelete: (tx: TransactionDto) => void;
  /** Filas de relleno mientras llegan los datos: se ven borrosas y no se pueden usar. */
  loading?: boolean;
}) {
  const money = useMoney();
  const [dir, setDir] = useState<"asc" | "desc">("desc");
  const sorted = useMemo(() => sortTransactions(rows, "date", dir), [rows, dir]);

  // Los enlaces y botones de dentro de la fila no deben abrir también el detalle.
  const stop = (event: MouseEvent) => event.stopPropagation();
  const rowKey = (tx: TransactionDto) => (event: KeyboardEvent) => {
    if (event.target === event.currentTarget && (event.key === "Enter" || event.key === " ")) {
      event.preventDefault();
      onOpen(tx);
    }
  };

  return (
    <div className="overflow-x-auto" inert={loading} aria-busy={loading}>
      <table className="w-full min-w-[44rem] border-collapse text-left text-sm">
        <caption className="sr-only">Historial de transacciones</caption>
        <thead>
          <tr className="border-y border-rule">
            <th scope="col" className="px-3 py-3 font-semibold">
              Tipo
            </th>
            <th scope="col" aria-sort={dir === "asc" ? "ascending" : "descending"} className="px-3 py-3 font-semibold">
              <button type="button" onClick={() => setDir((d) => (d === "asc" ? "desc" : "asc"))} className="inline-flex cursor-pointer items-center">
                Fecha
                <SortIcon state={dir} />
              </button>
            </th>
            {showCoin && (
              <th scope="col" className="px-3 py-3 font-semibold">
                Activos
              </th>
            )}
            <th scope="col" className="px-3 py-3 text-right font-semibold">
              Precio
            </th>
            <th scope="col" className="px-3 py-3 text-right font-semibold">
              Importe
            </th>
            <th scope="col" className="px-3 py-3 text-right font-semibold">
              Comisiones
            </th>
            <th scope="col" className="px-3 py-3 text-right font-semibold">
              Notas
            </th>
            <th scope="col" className="px-3 py-3 text-right font-semibold">
              Acciones
            </th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((tx) => {
            const direction = txDirection(tx.type);
            const sign = direction === 1 ? "+" : "-";
            const amount = txAmountUsd(tx);
            const label = typeLabel(tx.type);
            const when = dateFormat.format(new Date(tx.dateUtc));
            const description = `${label.toLowerCase()} de ${tx.name} del ${when}`;
            return (
              <tr
                key={tx.id}
                tabIndex={loading ? undefined : 0}
                onClick={loading ? undefined : () => onOpen(tx)}
                onKeyDown={loading ? undefined : rowKey(tx)}
                aria-label={loading ? undefined : `Ver el detalle de ${description}`}
                className={`border-b border-rule ${loading ? "animate-pulse select-none [&>td]:blur-sm" : `cursor-pointer ${ROW_HOVER}`}`}
              >
                <td className="px-3 py-3">
                  <span className="flex items-center gap-3 font-semibold">
                    <TypeBadge type={tx.type} />
                    {label}
                  </span>
                </td>
                <td className="whitespace-nowrap px-3 py-3 text-soft tabular-nums">{when}</td>
                {showCoin && (
                  <td className="px-3 py-3">
                    <Link
                      href={`/monedas/${tx.assetSlug}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      onClick={stop}
                      className="-mx-1 inline-flex items-center gap-2 rounded-lg px-1 py-1"
                    >
                      <CoinIcon url={tx.imageUrl} size={26} />
                      <span className="font-semibold">{tx.name}</span>
                      <span className="text-soft">{tx.symbol.toUpperCase()}</span>
                    </Link>
                  </td>
                )}
                <td className="px-3 py-3 text-right tabular-nums">{amount === null ? NONE : money.price(tx.unitPriceUsd)}</td>
                <td className="px-3 py-3 text-right tabular-nums">
                  <p className={`text-base font-semibold ${COLOR[direction]}`}>
                    {sign}
                    {formatQuantity(tx.quantity)} {tx.symbol.toUpperCase()}
                  </p>
                  {amount !== null && <p className="mt-0.5 text-xs text-soft">{money.price(amount)}</p>}
                </td>
                <td className="px-3 py-3 text-right tabular-nums">{tx.feeUsd > 0 ? money.price(tx.feeUsd) : NONE}</td>
                <td className="max-w-[10rem] truncate px-3 py-3 text-right" title={tx.note ?? undefined}>
                  {tx.note ?? NONE}
                </td>
                <td className="px-3 py-3 text-right">
                  <span className="inline-flex items-center gap-1">
                    <button
                      type="button"
                      onClick={(event) => {
                        stop(event);
                        onEdit(tx);
                      }}
                      aria-label={`Editar ${description}`}
                      title="Editar"
                      className={`${ICON_BUTTON} hover:text-ink`}
                    >
                      <PencilIcon />
                    </button>
                    <button
                      type="button"
                      onClick={(event) => {
                        stop(event);
                        onDelete(tx);
                      }}
                      aria-label={`Borrar ${description}`}
                      title="Borrar"
                      className={`${ICON_BUTTON} hover:text-down`}
                    >
                      <TrashIcon />
                    </button>
                  </span>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
