import assert from "node:assert/strict";
import { test } from "node:test";
import { formatCompactMoney, sparklinePath } from "../shared/lib/compact";

test("cifras grandes abreviadas", () => {
  assert.equal(formatCompactMoney(95_810_000_000, "USD", 1), "$95.81B");
  assert.equal(formatCompactMoney(5_850_000_000, "USD", 1), "$5.85B");
  assert.equal(formatCompactMoney(1_030_000_000, "USD", 1), "$1.03B");
  assert.equal(formatCompactMoney(null, "USD", 1), "--");
});

test("la línea pequeña sube y baja con los valores", () => {
  assert.equal(sparklinePath([1], 80, 28), null);
  const path = sparklinePath([1, 3, 2], 80, 28)!;
  assert.equal(path, "M0.0 26.0 L40.0 2.0 L80.0 14.0");
  assert.equal(sparklinePath([5, 5], 80, 28), "M0.0 14.0 L80.0 14.0");
});
