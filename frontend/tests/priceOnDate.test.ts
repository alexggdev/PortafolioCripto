import assert from "node:assert/strict";
import { test } from "node:test";
import { priceOnDate } from "../features/portfolio/lib/priceOnDate";

const points = [
  { date: "2026-01-01", priceUsd: 10 },
  { date: "2026-01-02", priceUsd: 12 },
  { date: "2026-01-05", priceUsd: 15 },
];

test("devuelve el cierre del mismo día", () => {
  assert.deepEqual(priceOnDate(points, "2026-01-02T18:30:00.000Z"), { status: "found", priceUsd: 12, date: "2026-01-02" });
});

test("un día sin dato usa el último anterior", () => {
  assert.deepEqual(priceOnDate(points, "2026-01-04T00:00:00.000Z"), { status: "found", priceUsd: 12, date: "2026-01-02" });
});

test("después del último dato usa el último", () => {
  assert.deepEqual(priceOnDate(points, "2026-02-01T00:00:00.000Z"), { status: "found", priceUsd: 15, date: "2026-01-05" });
});

test("antes del primer dato avisa de cuándo empieza", () => {
  assert.deepEqual(priceOnDate(points, "2025-12-31T23:59:00.000Z"), { status: "before", firstDate: "2026-01-01" });
});

test("sin histórico o con fecha rota no hay precio", () => {
  assert.deepEqual(priceOnDate([], "2026-01-01T00:00:00.000Z"), { status: "none" });
  assert.deepEqual(priceOnDate(points, "ayer"), { status: "none" });
  assert.deepEqual(priceOnDate([{ date: "2026-01-01", priceUsd: 0 }], "2026-01-01T00:00:00.000Z"), { status: "none" });
});
