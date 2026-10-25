import type { TransactionDto, TransactionType } from "../../../shared/lib/types";

// Cómo se muestra una transacción en la tabla: signo, importe y etiqueta. Sin React, para poder probarlo.

/** +1 si entran unidades a la cartera, −1 si salen. */
export function txDirection(type: TransactionType): 1 | -1 {
  return type === "Sell" || type === "TransferOut" || type === "Fee" ? -1 : 1;
}

/** Importe en USD (cantidad × precio), o null si la transacción no tiene precio (staking, airdrop, transferencias…). */
export function txAmountUsd(tx: Pick<TransactionDto, "quantity" | "unitPriceUsd" | "type">): number | null {
  if (tx.unitPriceUsd <= 0) return null;
  return tx.quantity * tx.unitPriceUsd;
}

/** Aspecto del círculo de la columna «Tipo»: intercambio (compra/venta), entrada o salida; verde si suman, rojo si restan. */
export function typeVisual(type: TransactionType): { glyph: "swap" | "in" | "out"; tone: "up" | "down" } {
  const tone = txDirection(type) === 1 ? "up" : "down";
  if (type === "Buy" || type === "Sell") return { glyph: "swap", tone };
  return { glyph: tone === "up" ? "in" : "out", tone };
}

export type TxSortKey = "date" | "price" | "amount" | "fee";

/** Ordena una copia de las transacciones. Sin importe o sin precio va al final. */
export function sortTransactions(txs: readonly TransactionDto[], key: TxSortKey, dir: "asc" | "desc"): TransactionDto[] {
  const sign = dir === "asc" ? 1 : -1;
  const pick = (t: TransactionDto): number | null => {
    switch (key) {
      case "date":
        return Date.parse(t.dateUtc);
      case "price":
        return t.unitPriceUsd > 0 ? t.unitPriceUsd : null;
      case "amount": {
        const a = txAmountUsd(t);
        return a === null ? null : a * txDirection(t.type);
      }
      case "fee":
        return t.feeUsd > 0 ? t.feeUsd : null;
    }
  };
  return [...txs].sort((a, b) => {
    const x = pick(a);
    const y = pick(b);
    if (x === null && y === null) return 0;
    if (x === null) return 1;
    if (y === null) return -1;
    return sign * (x - y);
  });
}
