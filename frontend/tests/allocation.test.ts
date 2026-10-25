import assert from "node:assert/strict";
import { test } from "node:test";
import { buildAllocation, buildPortfolioAllocation, formatSlicePct } from "../features/portfolio/lib/allocation";
import { bestAndWorst } from "../features/portfolio/lib/performers";
import type { PositionDto } from "../shared/lib/types";

function position(slug: string, valueUsd: number | null, pnlUsd: number | null = null, pnlPct: number | null = null): PositionDto {
  return {
    assetId: slug.length,
    slug,
    symbol: slug.slice(0, 3),
    name: slug.toUpperCase(),
    imageUrl: null,
    quantity: 1,
    averageCostUsd: 1,
    costBasisUsd: 1,
    priceUsd: valueUsd,
    priceSource: null,
    priceUpdatedAtUtc: null,
    valueUsd,
    unrealizedPnlUsd: pnlUsd,
    unrealizedPnlPct: pnlPct,
    realizedPnlUsd: 0,
    allocationPct: null,
    change24hPct: null,
  };
}

test("buildAllocation ordena por valor y suma 100", () => {
  const slices = buildAllocation([position("btc", 300), position("eth", 100), position("ada", 600)]);
  assert.deepEqual(slices.map((s) => s.key), ["ada", "btc", "eth"]);
  assert.equal(slices[0]!.pct, 60);
  assert.ok(Math.abs(slices.reduce((s, x) => s + x.pct, 0) - 100) < 1e-9);
});

test("buildAllocation ignora monedas sin precio o sin valor", () => {
  const slices = buildAllocation([position("btc", 100), position("sin", null), position("cero", 0)]);
  assert.deepEqual(slices.map((s) => s.key), ["btc"]);
  assert.deepEqual(buildAllocation([position("sin", null)]), []);
  assert.deepEqual(buildAllocation([]), []);
});

test("buildAllocation agrupa las pequeñas en «Otras»", () => {
  const many = Array.from({ length: 9 }, (_, i) => position(`c${i}`, 100 - i));
  const slices = buildAllocation(many, 6);
  assert.equal(slices.length, 7);
  assert.equal(slices[6]!.key, "otras");
  assert.equal(slices[6]!.valueUsd, 94 + 93 + 92);
  assert.ok(Math.abs(slices.reduce((s, x) => s + x.pct, 0) - 100) < 1e-9);
});

test("buildAllocation no agrupa una sola moneda sobrante", () => {
  const seven = Array.from({ length: 7 }, (_, i) => position(`c${i}`, 100 - i));
  const slices = buildAllocation(seven, 6);
  assert.equal(slices.length, 7);
  assert.ok(slices.every((s) => s.key !== "otras"));
});

test("bestAndWorst elige por porcentaje y salta las que no tienen precio", () => {
  const { best, worst } = bestAndWorst([
    position("btc", 100, 50, 10),
    position("eth", 100, -30, -60),
    position("sin", null, null, null),
    position("ada", 100, 5, 2),
  ]);
  assert.equal(best?.slug, "btc");
  assert.equal(worst?.slug, "eth");
  assert.equal(worst?.pnlUsd, -30);
});

test("bestAndWorst con una sola moneda devuelve la misma; sin datos, nulos", () => {
  const one = bestAndWorst([position("atom", 98, -412, -80)]);
  assert.equal(one.best?.slug, "atom");
  assert.equal(one.worst?.slug, "atom");
  assert.deepEqual(bestAndWorst([position("sin", null)]), { best: null, worst: null });
});

test("buildPortfolioAllocation reparte por portafolio e ignora los vacíos", () => {
  const base = { positionCount: 1, costBasisUsd: 0, unrealizedPnlUsd: 0, unrealizedPnlPct: null, realizedPnlUsd: 0, change24hUsd: 0, change24hPct: null, includeInTotal: true };
  const slices = buildPortfolioAllocation([
    { id: "a", name: "Pequeño", valueUsd: 25, ...base },
    { id: "b", name: "Grande", valueUsd: 75, ...base },
    { id: "c", name: "Vacío", valueUsd: 0, ...base },
  ]);
  assert.deepEqual(slices.map((s) => [s.label, s.pct]), [["Grande", 75], ["Pequeño", 25]]);
  assert.deepEqual(buildPortfolioAllocation([]), []);
});

test("formatSlicePct usa coma y avisa de lo casi nulo", () => {
  assert.equal(formatSlicePct(99.2106), "99,21 %");
  assert.equal(formatSlicePct(0.004), "<0,01 %");
});
