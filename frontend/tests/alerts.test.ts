import assert from "node:assert/strict";
import { test } from "node:test";
import { directionLabel, distanceToTargetPct, splitAlerts, validateAlertForm } from "../features/alerts/lib/alerts";
import type { PriceAlert } from "../shared/lib/types";

const base = { assetSlug: "bitcoin", direction: "Above" as const, price: "70000" };

test("validateAlertForm genera el JSON con el precio tal cual", () => {
  const { errors, body } = validateAlertForm({ ...base, price: "70.000,5" });
  assert.deepEqual(errors, {});
  assert.equal(body, '{"assetSlug":"bitcoin","direction":"Above","targetPriceUsd":70000.5}');
});

test("validateAlertForm no pierde decimales pequeños", () => {
  const { body } = validateAlertForm({ ...base, direction: "Below", price: "0,000000000000000001" });
  assert.equal(body, '{"assetSlug":"bitcoin","direction":"Below","targetPriceUsd":0.000000000000000001}');
});

test("validateAlertForm detecta los errores del formulario", () => {
  assert.ok(validateAlertForm({ ...base, assetSlug: "  " }).errors.asset);
  assert.ok(validateAlertForm({ ...base, price: "" }).errors.price);
  assert.ok(validateAlertForm({ ...base, price: "abc" }).errors.price);
  assert.ok(validateAlertForm({ ...base, price: "0" }).errors.price);
  assert.ok(validateAlertForm({ ...base, price: "-5" }).errors.price);
  assert.ok(validateAlertForm({ ...base, price: "12345678901" }).errors.price); // 11 cifras enteras
  assert.ok(validateAlertForm({ ...base, price: "0,1234567890123456789" }).errors.price); // 19 decimales
  assert.equal(validateAlertForm({ ...base, price: "abc" }).body, null);
});

test("distanceToTargetPct: positivo si hay que subir, negativo si hay que bajar", () => {
  assert.equal(distanceToTargetPct(100, 150), 50);
  assert.ok(Math.abs((distanceToTargetPct(100, 80) ?? 0) + 20) < 1e-9);
  assert.equal(distanceToTargetPct(null, 80), null);
  assert.equal(distanceToTargetPct(0, 80), null);
});

test("directionLabel", () => {
  assert.equal(directionLabel("Above"), "Sube hasta");
  assert.equal(directionLabel("Below"), "Baja hasta");
});

const alert = (id: number, triggeredAtUtc: string | null): PriceAlert => ({
  id, assetSlug: "bitcoin", symbol: "btc", name: "Bitcoin", imageUrl: null, direction: "Above",
  targetPriceUsd: 100, currentPriceUsd: null, createdAtUtc: "2026-10-01T00:00:00Z",
  triggeredAtUtc, triggeredPriceUsd: triggeredAtUtc ? 101 : null, unread: false,
});

test("splitAlerts separa activas y disparadas y las ordena", () => {
  const { active, triggered } = splitAlerts([
    alert(1, null),
    alert(2, "2026-10-10T10:00:00Z"),
    alert(3, null),
    alert(4, "2026-10-12T10:00:00Z"),
  ]);
  assert.deepEqual(active.map((a) => a.id), [3, 1]);
  assert.deepEqual(triggered.map((a) => a.id), [4, 2]);
});
