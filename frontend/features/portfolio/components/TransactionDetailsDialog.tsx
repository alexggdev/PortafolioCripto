"use client";

import { useId, type ReactNode } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { Modal } from "@/shared/components/Modal";
import { formatQuantity } from "@/shared/lib/format";
import { txAmountUsd, txDirection } from "@/features/portfolio/lib/transactionRows";
import { typeLabel } from "@/features/portfolio/lib/transactions";
import type { TransactionDto } from "@/shared/lib/types";

const dateFormat = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-soft">{label}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  );
}

/** Detalle de una transacción (solo lectura): tipo, fecha, precio, cantidad, comisión, total y notas. */
export function TransactionDetailsDialog({ tx, onClose }: { tx: TransactionDto; onClose: () => void }) {
  const money = useMoney();
  const titleId = useId();
  const amount = txAmountUsd(tx);
  // Igual que en el formulario: una compra suma la comisión y una venta la resta.
  const total = amount === null ? null : tx.type === "Buy" ? amount + tx.feeUsd : tx.type === "Sell" ? amount - tx.feeUsd : amount;
  const totalLabel = tx.type === "Buy" ? "Total gastado" : tx.type === "Sell" ? "Total recibido" : "Total";

  return (
    <Modal labelId={titleId} onClose={onClose} width="30rem">
      <h2 id={titleId} className="pr-10 text-2xl font-bold">
        Detalles de la transacción
      </h2>
      <dl className="mt-6 space-y-5 px-1">
        <Row label="Tipo">{typeLabel(tx.type)}</Row>
        <Row label="Fecha">{dateFormat.format(new Date(tx.dateUtc))}</Row>
        <Row label="Precio por moneda">{tx.unitPriceUsd > 0 ? money.price(tx.unitPriceUsd) : "--"}</Row>
        <Row label="Cantidad">
          {txDirection(tx.type) === 1 ? "" : "−"}
          {formatQuantity(tx.quantity)} {tx.symbol.toUpperCase()}
        </Row>
        <Row label="Comisiones">{tx.feeUsd > 0 ? money.price(tx.feeUsd) : "Sin comisión"}</Row>
        <Row label={totalLabel}>{total === null ? "--" : money.price(total)}</Row>
        <div>
          <dt className="text-soft">Notas</dt>
          <dd className="mt-1 whitespace-pre-wrap break-words">{tx.note?.trim() ? tx.note : "--"}</dd>
        </div>
      </dl>
    </Modal>
  );
}
