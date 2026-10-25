import assert from "node:assert/strict";
import { test } from "node:test";
import { formatUsd, formatUsdWhole } from "../shared/lib/format";
import { convertUsd, formatMoney, formatMoneyValue, formatMoneyWhole } from "../features/currency/lib/money";

const NBSP = / /g;
const plain = (s: string) => s.replace(NBSP, " ");

test("en USD con tipo 1 coincide exactamente con formatUsd", () => {
  for (const v of [83554.12, 1.0002, 0.5, 0.00001234, 1234567.891]) {
    assert.equal(formatMoney(v, "USD", 1), formatUsd(v));
  }
  assert.equal(formatMoneyWhole(1660234123456.7, "USD", 1), formatUsdWhole(1660234123456.7));
});

test("convierte multiplicando por el tipo de cambio", () => {
  assert.equal(convertUsd(100, 0.86), 86);
  assert.equal(convertUsd(null, 0.86), null);
  assert.equal(convertUsd(undefined, 0.86), null);
  assert.equal(convertUsd(Number.NaN, 0.86), null);
});

test("euros: coma decimal, punto de miles y símbolo €", () => {
  assert.equal(plain(formatMoney(83554.12, "EUR", 0.86)), "71.856,54 €");
});

test("pesos chilenos: enteros desde 100, decimales por debajo", () => {
  assert.equal(plain(formatMoney(83554.12, "CLP", 950)), "79.376.414 CLP");
  assert.equal(plain(formatMoneyValue(350, "CLP")), "350 CLP");
  assert.match(plain(formatMoneyValue(0.0123, "CLP")), /^0,0123 CLP$/);
});

test("soles: dos decimales para precios grandes y más para céntimos", () => {
  assert.equal(plain(formatMoneyValue(284900.333, "PEN")), "284.900,33 PEN");
  assert.match(plain(formatMoneyValue(0.00000987, "PEN")), /^0,00000987 PEN$/);
});

test("importes enteros (capitalización) sin decimales en cualquier moneda", () => {
  assert.equal(plain(formatMoneyWhole(1_000_000, "EUR", 0.86)), "860.000 €");
});

test("sin valor devuelve la raya", () => {
  assert.equal(formatMoney(null, "EUR", 0.86), "—");
  assert.equal(formatMoneyWhole(undefined, "CLP", 950), "—");
});
