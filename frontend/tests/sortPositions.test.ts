import assert from "node:assert/strict";
import { test } from "node:test";
import { nextSort, sortPositions } from "../features/portfolio/lib/sortPositions";
import type { PositionDto } from "../shared/lib/types";

const pos = (symbol: string, over: Partial<PositionDto>): PositionDto =>
  ({
    assetId: 1, slug: symbol.toLowerCase(), symbol, name: symbol, imageUrl: null, quantity: 1, averageCostUsd: 1,
    costBasisUsd: 1, priceUsd: 1, priceSource: null, priceUpdatedAtUtc: null, valueUsd: 1, unrealizedPnlUsd: 0,
    unrealizedPnlPct: 0, realizedPnlUsd: 0, allocationPct: null, change24hPct: 0, ...over,
  }) as PositionDto;

test("ordena por valor de mayor a menor y deja lo que no tiene valor al final", () => {
  const r = sortPositions([pos("A", { valueUsd: 10 }), pos("B", { valueUsd: null }), pos("C", { valueUsd: 50 })], "value", "desc");
  assert.deepEqual(r.map((p) => p.symbol), ["C", "A", "B"]);
});

test("sin valor va al final también en orden ascendente", () => {
  const r = sortPositions([pos("A", { valueUsd: 10 }), pos("B", { valueUsd: null }), pos("C", { valueUsd: 50 })], "value", "asc");
  assert.deepEqual(r.map((p) => p.symbol), ["A", "C", "B"]);
});

test("ordena por nombre sin distinguir mayúsculas", () => {
  const r = sortPositions([pos("x", { name: "zcash" }), pos("y", { name: "Aave" })], "name", "asc");
  assert.deepEqual(r.map((p) => p.name), ["Aave", "zcash"]);
});

test("la variación de 1 h puede faltar", () => {
  const r = sortPositions([pos("A", {}), pos("B", { change1hPct: 2 })], "change1h", "desc");
  assert.deepEqual(r.map((p) => p.symbol), ["B", "A"]);
});

test("no modifica la lista original", () => {
  const list = [pos("A", { valueUsd: 1 }), pos("B", { valueUsd: 2 })];
  sortPositions(list, "value", "desc");
  assert.equal(list[0]!.symbol, "A");
});

test("pulsar otra columna empieza descendente; el nombre, ascendente; la misma invierte", () => {
  assert.deepEqual(nextSort({ key: "value", dir: "desc" }, "price"), { key: "price", dir: "desc" });
  assert.deepEqual(nextSort({ key: "value", dir: "desc" }, "name"), { key: "name", dir: "asc" });
  assert.deepEqual(nextSort({ key: "value", dir: "desc" }, "value"), { key: "value", dir: "asc" });
});

test("si la otra columna dejaría el mismo orden, la primera pulsación empieza al revés", async () => {
  const { nextVisibleSort } = await import("../features/portfolio/lib/sortPositions");
  const list = [pos("A", { assetId: 1, valueUsd: 50, priceUsd: 5 }), pos("B", { assetId: 2, valueUsd: 10, priceUsd: 1 })];
  assert.deepEqual(nextVisibleSort(list, { key: "value", dir: "desc" }, "price"), { key: "price", dir: "asc" });
  const crossed = [pos("A", { assetId: 1, valueUsd: 50, priceUsd: 1 }), pos("B", { assetId: 2, valueUsd: 10, priceUsd: 5 })];
  assert.deepEqual(nextVisibleSort(crossed, { key: "value", dir: "desc" }, "price"), { key: "price", dir: "desc" });
  assert.deepEqual(nextVisibleSort(list, { key: "value", dir: "desc" }, "value"), { key: "value", dir: "asc" });
});
