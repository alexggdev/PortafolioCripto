import assert from "node:assert/strict";
import { test } from "node:test";
import { compareWithBtc } from "../features/portfolio/lib/benchmark";

const h = (date: string, valueUsd: number, netContributedUsd: number) => ({ date, valueUsd, netContributedUsd });
const b = (date: string, priceUsd: number) => ({ date, priceUsd });

test("sin ingresos nuevos, la rentabilidad es la variación del valor", () => {
  const c = compareWithBtc(
    [h("2026-01-01", 100, 100), h("2026-01-02", 110, 100), h("2026-01-03", 99, 100)],
    [b("2026-01-01", 10), b("2026-01-02", 12), b("2026-01-03", 11)],
  )!;
  assert.deepEqual(c.portfolioPct.map((x) => Math.round(x * 100) / 100), [0, 10, -1]);
  assert.deepEqual(c.btcPct.map((x) => Math.round(x * 100) / 100), [0, 20, 10]);
});

test("ingresar dinero no cuenta como ganancia", () => {
  const c = compareWithBtc(
    [h("2026-01-01", 100, 100), h("2026-01-02", 1100, 1100)],
    [b("2026-01-01", 10), b("2026-01-02", 10)],
  )!;
  assert.equal(c.portfolioPct[1], 0);
});

test("retirar dinero tampoco cuenta como pérdida", () => {
  const c = compareWithBtc(
    [h("2026-01-01", 200, 200), h("2026-01-02", 105, 100)],
    [b("2026-01-01", 10), b("2026-01-02", 10)],
  )!;
  assert.ok(Math.abs(c.portfolioPct[1] - 2.5) < 1e-9); // (105 + 100 de retirada)/200
});

test("empieza el primer día con valor y mide Bitcoin desde ahí", () => {
  const c = compareWithBtc(
    [h("2026-01-01", 0, 0), h("2026-01-02", 100, 100), h("2026-01-03", 100, 100)],
    [b("2026-01-01", 5), b("2026-01-02", 10), b("2026-01-03", 20)],
  )!;
  assert.equal(c.times.length, 2);
  assert.equal(c.btcPct[1], 100);
});

test("un día sin precio de Bitcoin usa el último conocido", () => {
  const c = compareWithBtc(
    [h("2026-01-01", 100, 100), h("2026-01-02", 100, 100), h("2026-01-03", 100, 100)],
    [b("2026-01-01", 10), b("2026-01-03", 20)],
  )!;
  assert.deepEqual(c.btcPct, [0, 0, 100]);
});

test("sin datos suficientes devuelve null", () => {
  assert.equal(compareWithBtc([h("2026-01-01", 100, 100)], [b("2026-01-01", 10)]), null);
  assert.equal(compareWithBtc([h("2026-01-01", 100, 100), h("2026-01-02", 100, 100)], []), null);
});

test("la ganancia en dólares es valor menos lo aportado", () => {
  const c = compareWithBtc(
    [h("2026-01-01", 100, 100), h("2026-01-02", 130, 110)],
    [b("2026-01-01", 10), b("2026-01-02", 10)],
  )!;
  assert.deepEqual(c.profitUsd, [0, 20]);
});
