import assert from "node:assert/strict";
import { test } from "node:test";
import { gainAt, historyDomain, periodProfitPct, profitTone, sumHistories, toHistorySeries } from "../features/portfolio/lib/portfolioHistory";
import type { PortfolioHistory, PortfolioHistoryPoint } from "../shared/lib/types";

const point = (date: string, valueUsd: number, netContributedUsd: number): PortfolioHistoryPoint => ({
  date,
  valueUsd,
  netContributedUsd,
});

test("toHistorySeries convierte fechas UTC y multiplica por el tipo de cambio", () => {
  const s = toHistorySeries([point("2026-10-01", 100, 80), point("2026-10-02", 110, 80)], 0.5);
  assert.deepEqual(s.times, [Date.UTC(2026, 9, 1), Date.UTC(2026, 9, 2)]);
  assert.deepEqual(s.value, [50, 55]);
  assert.deepEqual(s.contributed, [40, 40]);
});

test("historyDomain con todo positivo no baja de cero y abarca las dos líneas", () => {
  const [lo, hi] = historyDomain([100, 120], [90, 90]);
  assert.ok(lo >= 0 && lo < 90);
  assert.ok(hi > 120);
});

test("historyDomain deja bajar el suelo si lo aportado es negativo", () => {
  const [lo, hi] = historyDomain([10, 20], [-50, -50]);
  assert.ok(lo < -50);
  assert.ok(hi > 20);
});

test("historyDomain sin datos devuelve un dominio válido", () => {
  assert.deepEqual(historyDomain([], []), [0, 1]);
});

test("periodProfitPct usa valor inicial más dinero añadido como base", () => {
  const points = [point("a", 214, 200), point("b", 360, 300)];
  // ganancia 46 sobre 214 + 100 = 314
  assert.ok(Math.abs((periodProfitPct(points, 46) ?? 0) - (46 / 314) * 100) < 1e-9);
});

test("periodProfitPct es null sin base positiva o sin datos", () => {
  assert.equal(periodProfitPct([point("a", 0, 0), point("b", 0, 0)], 0), null);
  assert.equal(periodProfitPct([point("a", 1, 1)], 0), null);
  assert.equal(periodProfitPct([point("a", 1, 1), point("b", 2, 1)], null), null);
});

test("gainAt es valor menos aportado y null fuera de rango", () => {
  const s = toHistorySeries([point("2026-10-01", 100, 80)], 1);
  assert.equal(gainAt(s, 0), 20);
  assert.equal(gainAt(s, 1), null);
  assert.equal(gainAt(s, -1), null);
});

const history = (points: PortfolioHistoryPoint[], profit: number | null, first: string | null = null): PortfolioHistory => ({
  portfolioId: null,
  range: "1M",
  firstTransactionDate: first,
  points,
  periodProfitUsd: profit,
  includesCurrentValue: false,
});

test("sumHistories suma por día y un portafolio aún sin historia aporta 0", () => {
  const a = history([point("2026-10-01", 100, 90), point("2026-10-02", 110, 90), point("2026-10-03", 120, 90)], 30, "2026-09-01");
  const b = history([point("2026-10-02", 10, 10), point("2026-10-03", 12, 10)], 2, "2026-10-02");
  const sum = sumHistories([a, b])!;
  assert.deepEqual(sum.points, [
    point("2026-10-01", 100, 90),
    point("2026-10-02", 120, 100),
    point("2026-10-03", 132, 100),
  ]);
  assert.equal(sum.periodProfitUsd, 32);
  assert.equal(sum.firstTransactionDate, "2026-09-01");
});

test("sumHistories conserva el último dato cuando a un portafolio le falta un día", () => {
  const a = history([point("2026-10-01", 100, 100), point("2026-10-03", 130, 100)], null);
  const b = history([point("2026-10-01", 1, 1), point("2026-10-02", 2, 1), point("2026-10-03", 3, 1)], null);
  const sum = sumHistories([a, b])!;
  assert.equal(sum.points[1].valueUsd, 102);
  assert.equal(sum.periodProfitUsd, null);
  assert.equal(sumHistories([]), null);
});

test("profitTone es rojo solo con pérdida", () => {
  assert.equal(profitTone(-0.01), "down");
  assert.equal(profitTone(0), "up");
  assert.equal(profitTone(5), "up");
  assert.equal(profitTone(null), "up");
});
