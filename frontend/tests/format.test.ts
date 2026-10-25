import assert from "node:assert/strict";
import { test } from "node:test";
import { directionOf, formatAgo, formatPct, formatUsd, formatUsdWhole, NO_VALUE } from "../shared/lib/format";

const NBSP = " ";
const MINUS = "−";

test("formatUsd usa más decimales cuanto más barata es la moneda", () => {
  assert.equal(formatUsd(83554.123), `83.554,12${NBSP}US$`);
  assert.equal(formatUsd(1850), `1.850,00${NBSP}US$`); // agrupa también los números de cuatro cifras
  assert.equal(formatUsd(0.5234), `0,5234${NBSP}US$`);
  assert.equal(formatUsd(0.00001234), `0,00001234${NBSP}US$`);
});

test("formatUsd y formatUsdWhole devuelven raya cuando no hay dato", () => {
  assert.equal(formatUsd(null), NO_VALUE);
  assert.equal(formatUsd(undefined), NO_VALUE);
  assert.equal(formatUsd(Number.NaN), NO_VALUE);
  assert.equal(formatUsdWhole(null), NO_VALUE);
});

test("formatUsdWhole da el importe entero completo", () => {
  assert.equal(formatUsdWhole(1660234123456.78), `1.660.234.123.457${NBSP}US$`);
});

test("formatPct lleva signo explícito y dos decimales", () => {
  assert.equal(formatPct(1.234), `+1,23${NBSP}%`);
  assert.equal(formatPct(-4.5), `${MINUS}4,50${NBSP}%`);
  assert.equal(formatPct(0), `0,00${NBSP}%`);
  assert.equal(formatPct(null), NO_VALUE);
});

test("un valor que se muestra como 0,00 no es subida ni bajada", () => {
  assert.equal(directionOf(0.004), "flat");
  assert.equal(directionOf(-0.004), "flat");
  assert.equal(formatPct(-0.004), `0,00${NBSP}%`);
  assert.equal(directionOf(0.006), "up");
  assert.equal(directionOf(-0.006), "down");
  assert.equal(directionOf(null), "flat");
});

test("formatAgo elige la unidad adecuada", () => {
  assert.equal(formatAgo(4.7), `hace 4${NBSP}s`);
  assert.equal(formatAgo(-3), `hace 0${NBSP}s`);
  assert.equal(formatAgo(125), `hace 2${NBSP}min`);
  assert.equal(formatAgo(3 * 3600 + 10), `hace 3${NBSP}h`);
  assert.equal(formatAgo(3 * 86400), `hace 3${NBSP}d`);
});
