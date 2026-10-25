import assert from "node:assert/strict";
import { test } from "node:test";
import { sortTransactions, txAmountUsd, txDirection, typeVisual } from "../features/portfolio/lib/transactionRows";
import type { TransactionDto } from "../shared/lib/types";

const tx = (over: Partial<TransactionDto>): TransactionDto =>
  ({ id: 1, assetSlug: "cosmos", symbol: "ATOM", name: "Cosmos", imageUrl: null, type: "Buy", quantity: 2, unitPriceUsd: 10, feeUsd: 0, dateUtc: "2026-01-01T00:00:00Z", note: null, ...over }) as TransactionDto;

test("compras y entradas suman; ventas, salidas y comisiones restan", () => {
  assert.equal(txDirection("Buy"), 1);
  assert.equal(txDirection("TransferIn"), 1);
  assert.equal(txDirection("Staking"), 1);
  assert.equal(txDirection("Airdrop"), 1);
  assert.equal(txDirection("Sell"), -1);
  assert.equal(txDirection("TransferOut"), -1);
  assert.equal(txDirection("Fee"), -1);
});

test("el importe es cantidad por precio; sin precio no hay importe", () => {
  assert.equal(txAmountUsd(tx({})), 20);
  assert.equal(txAmountUsd(tx({ unitPriceUsd: 0 })), null);
});

test("el círculo del tipo: intercambio en compra y venta, flecha en transferencias, verde o rojo según suma o resta", () => {
  assert.deepEqual(typeVisual("Buy"), { glyph: "swap", tone: "up" });
  assert.deepEqual(typeVisual("Sell"), { glyph: "swap", tone: "down" });
  assert.deepEqual(typeVisual("TransferIn"), { glyph: "in", tone: "up" });
  assert.deepEqual(typeVisual("TransferOut"), { glyph: "out", tone: "down" });
  assert.deepEqual(typeVisual("Staking"), { glyph: "in", tone: "up" });
});

test("ordena por fecha y por importe con signo; sin importe va al final", () => {
  const a = tx({ id: 1, dateUtc: "2026-01-01T00:00:00Z" });
  const b = tx({ id: 2, dateUtc: "2026-03-01T00:00:00Z", type: "Sell" });
  const c = tx({ id: 3, dateUtc: "2026-02-01T00:00:00Z", unitPriceUsd: 0, type: "Airdrop" });
  assert.deepEqual(sortTransactions([a, b, c], "date", "desc").map((t) => t.id), [2, 3, 1]);
  assert.deepEqual(sortTransactions([a, b, c], "amount", "desc").map((t) => t.id), [1, 2, 3]);
  assert.deepEqual(sortTransactions([a, b, c], "amount", "asc").map((t) => t.id), [2, 1, 3]);
});
