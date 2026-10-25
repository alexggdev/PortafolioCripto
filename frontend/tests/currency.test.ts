import assert from "node:assert/strict";
import { test } from "node:test";
import {
  availableCurrencies,
  parseCurrency,
  resolveCurrency,
  sanitizeRates,
  USD_ONLY,
} from "../features/currency/lib/currency";

test("parseCurrency acepta monedas conocidas sin distinguir mayúsculas y cae a USD", () => {
  assert.equal(parseCurrency("eur"), "EUR");
  assert.equal(parseCurrency(" PEN "), "PEN");
  assert.equal(parseCurrency("CLP"), "CLP");
  assert.equal(parseCurrency("JPY"), "USD");
  assert.equal(parseCurrency(""), "USD");
  assert.equal(parseCurrency(undefined), "USD");
  assert.equal(parseCurrency("EUR; drop table"), "USD");
});

test("sanitizeRates se queda con monedas conocidas y tipos válidos", () => {
  const fx = sanitizeRates({
    base: "USD",
    rates: { USD: 5, EUR: 0.86, PEN: 3.4, CLP: 950, JPY: 150 },
    updatedAtUtc: "2026-10-14T00:00:01Z",
  });
  assert.deepEqual(fx.rates, { USD: 1, EUR: 0.86, PEN: 3.4, CLP: 950 });
  assert.equal(fx.updatedAtUtc, "2026-10-14T00:00:01Z");
});

test("sanitizeRates descarta tipos no positivos, no numéricos o infinitos", () => {
  const fx = sanitizeRates({ rates: { EUR: 0, PEN: -1, CLP: "950", USD: 1 }, updatedAtUtc: "mañana" });
  assert.deepEqual(fx.rates, { USD: 1 });
  assert.equal(fx.updatedAtUtc, null);
  assert.deepEqual(sanitizeRates({ rates: { EUR: Number.POSITIVE_INFINITY } }).rates, { USD: 1 });
});

test("sanitizeRates con basura devuelve solo USD", () => {
  assert.deepEqual(sanitizeRates(null), USD_ONLY);
  assert.deepEqual(sanitizeRates("x"), USD_ONLY);
  assert.deepEqual(sanitizeRates({}), USD_ONLY);
  assert.deepEqual(sanitizeRates({ rates: null }), USD_ONLY);
});

test("availableCurrencies respeta el orden USD, EUR, PEN, CLP", () => {
  assert.deepEqual(availableCurrencies({ rates: { CLP: 950, USD: 1, EUR: 0.86 }, updatedAtUtc: null }), ["USD", "EUR", "CLP"]);
});

test("resolveCurrency usa el tipo pedido o cae a USD avisando", () => {
  const fx = { rates: { USD: 1, EUR: 0.86 }, updatedAtUtc: null };
  assert.deepEqual(resolveCurrency("EUR", fx), { currency: "EUR", rate: 0.86, fallback: false });
  assert.deepEqual(resolveCurrency("PEN", fx), { currency: "USD", rate: 1, fallback: true });
  assert.deepEqual(resolveCurrency("USD", USD_ONLY), { currency: "USD", rate: 1, fallback: false });
});
