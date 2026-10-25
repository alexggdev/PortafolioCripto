import assert from "node:assert/strict";
import { test } from "node:test";
import { formatQuantity, toPlainNumber } from "../shared/lib/format";
import { formatSignedMoney } from "../features/currency/lib/money";
import {
  localToUtcIso,
  normalizeDecimal,
  priceMode,
  utcIsoToLocal,
  validateTransactionForm,
  type TransactionFormValues,
} from "../features/portfolio/lib/transactions";

const NOW = Date.parse("2026-10-15T12:00:00Z");
const plain = (s: string) => s.replace(/\u00a0/g, " ");

test("normalizeDecimal: un separador es el decimal; dos distintos, el último", () => {
  assert.equal(normalizeDecimal("1,5"), "1.5");
  assert.equal(normalizeDecimal("1.5"), "1.5");
  assert.equal(normalizeDecimal("1.234,56"), "1234.56");
  assert.equal(normalizeDecimal("1,234.56"), "1234.56");
  assert.equal(normalizeDecimal("1.234.567"), "1234567");
  assert.equal(normalizeDecimal("0,50"), "0.5");
  assert.equal(normalizeDecimal("007"), "7");
  assert.equal(normalizeDecimal(".5"), "0.5");
  assert.equal(normalizeDecimal("0.000000000001"), "0.000000000001");
});

test("normalizeDecimal: rechaza lo que no es un número", () => {
  for (const bad of ["", "  ", "abc", "-1", "1e5", "1,2,3", "1.2.3,4", "1..2", ",", "1,234,56"]) {
    assert.equal(normalizeDecimal(bad), null, bad);
  }
});

const base: TransactionFormValues = {
  assetSlug: "bitcoin",
  type: "Buy",
  quantity: "0,5",
  price: "60000",
  fee: "",
  date: "2026-10-01T10:30",
  note: "",
};

test("compra válida: JSON con los números tal cual, sin coma flotante", () => {
  const { errors, body } = validateTransactionForm({ ...base, quantity: "0,123456789012", price: "0,000000000000000001" }, NOW);
  assert.deepEqual(errors, {});
  const parsed = body as string;
  assert.match(parsed, /"quantity":0\.123456789012,/);
  assert.match(parsed, /"unitPriceUsd":0\.000000000000000001,/);
  const json = JSON.parse(parsed);
  assert.equal(json.assetSlug, "bitcoin");
  assert.equal(json.type, "Buy");
  assert.equal(json.feeUsd, undefined);
  assert.equal(json.note, undefined);
  assert.match(json.dateUtc, /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/);
});

test("compra y venta exigen precio > 0; staking y airdrop no envían precio", () => {
  assert.ok(validateTransactionForm({ ...base, price: "" }, NOW).errors.price);
  assert.ok(validateTransactionForm({ ...base, price: "0" }, NOW).errors.price);
  assert.ok(validateTransactionForm({ ...base, type: "Sell", price: "" }, NOW).errors.price);

  const transferOut = validateTransactionForm({ ...base, type: "TransferOut", price: "123" }, NOW);
  assert.deepEqual(transferOut.errors, {});
  assert.equal(JSON.parse(transferOut.body as string).unitPriceUsd, undefined);

  const transferIn = validateTransactionForm({ ...base, type: "TransferIn", price: "50" }, NOW);
  assert.deepEqual(transferIn.errors, {});
  assert.equal(JSON.parse(transferIn.body as string).unitPriceUsd, undefined);
  assert.equal(priceMode("TransferIn"), "none");
});

test("límites de cantidad, precio y comisión como en el servidor", () => {
  assert.ok(validateTransactionForm({ ...base, quantity: "0" }, NOW).errors.quantity);
  assert.ok(validateTransactionForm({ ...base, quantity: "" }, NOW).errors.quantity);
  assert.ok(validateTransactionForm({ ...base, quantity: "abc" }, NOW).errors.quantity);
  assert.ok(validateTransactionForm({ ...base, quantity: "0,1234567890123" }, NOW).errors.quantity); // 13 decimales
  assert.ok(validateTransactionForm({ ...base, quantity: "1000000000000000" }, NOW).errors.quantity); // 16 cifras
  assert.ok(validateTransactionForm({ ...base, fee: "0,123456789" }, NOW).errors.fee); // 9 decimales
  assert.ok(validateTransactionForm({ ...base, fee: "x" }, NOW).errors.fee);
  assert.deepEqual(validateTransactionForm({ ...base, fee: "1,5" }, NOW).errors, {});
});

test("moneda obligatoria, nota máxima y fechas", () => {
  assert.ok(validateTransactionForm({ ...base, assetSlug: "" }, NOW).errors.asset);
  assert.ok(validateTransactionForm({ ...base, note: "x".repeat(201) }, NOW).errors.note);
  assert.ok(validateTransactionForm({ ...base, date: "" }, NOW).errors.date);
  assert.ok(validateTransactionForm({ ...base, date: "2030-01-01T00:00" }, NOW).errors.date); // futura
  assert.ok(validateTransactionForm({ ...base, date: "2008-12-31T00:00" }, NOW).errors.date); // antes de Bitcoin
});

test("la nota y la comisión se envían solo si existen; la nota va escapada", () => {
  const { body } = validateTransactionForm({ ...base, fee: "2", note: ' dijo "hola"\n ' }, NOW);
  const json = JSON.parse(body as string);
  assert.equal(json.feeUsd, 2);
  assert.equal(json.note, 'dijo "hola"');
});

test("fecha local ↔ UTC es reversible", () => {
  const iso = localToUtcIso("2026-10-01T10:30");
  assert.ok(iso);
  assert.equal(utcIsoToLocal(iso as string), "2026-10-01T10:30");
  assert.equal(localToUtcIso("no es fecha"), null);
  assert.equal(localToUtcIso("2026-13-45T10:30"), null);
});

test("formatQuantity y toPlainNumber", () => {
  assert.equal(formatQuantity(0.5), "0,5");
  assert.equal(plain(formatQuantity(1250)), "1.250");
  assert.equal(formatQuantity(0.00012345), "0,00012345");
  assert.equal(formatQuantity(0.000000001), "0,000000001");
  assert.equal(formatQuantity(null), "—");
  assert.equal(toPlainNumber(0.00000987), "0.00000987");
  assert.equal(toPlainNumber(1234567.5), "1234567.5");
});

test("formatSignedMoney: signo explícito y cero sin signo", () => {
  assert.equal(plain(formatSignedMoney(100, "EUR", 0.86)), "+86,00 €");
  assert.equal(plain(formatSignedMoney(-100, "EUR", 0.86)), "\u221286,00 €");
  assert.ok(!/[+\u2212]/.test(formatSignedMoney(0.001, "USD", 1))); // 0,001 se ve como cero: sin signo
  assert.ok(!/[+\u2212]/.test(formatSignedMoney(0, "USD", 1)));
  assert.equal(formatSignedMoney(null, "USD", 1), "—");
});

test("validateTransactionForm: incluye portfolioId solo si es un GUID válido", () => {
  const id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
  const withId = validateTransactionForm(base, NOW, id);
  assert.deepEqual(withId.errors, {});
  assert.equal(JSON.parse(withId.body ?? "{}").portfolioId, id);

  const bad = validateTransactionForm(base, NOW, "no-es-guid");
  assert.equal(JSON.parse(bad.body ?? "{}").portfolioId, undefined);

  const none = validateTransactionForm(base, NOW);
  assert.equal(JSON.parse(none.body ?? "{}").portfolioId, undefined);
});

test("en una venta la comisión no puede superar lo que se recibe", () => {
  const sale = { ...base, type: "Sell" as const, quantity: "1", price: "100" };
  assert.ok(validateTransactionForm({ ...sale, fee: "150" }, NOW).errors.fee);
  assert.deepEqual(validateTransactionForm({ ...sale, fee: "99" }, NOW).errors, {});
  assert.deepEqual(validateTransactionForm({ ...base, fee: "150" }, NOW).errors, {}); // en una compra sí puede
});
