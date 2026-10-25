import assert from "node:assert/strict";
import { test } from "node:test";
import { parseRange, RANGES } from "../features/market/lib/ranges";
import { isValidSlug } from "../features/market/lib/slug";

test("parseRange acepta códigos sin distinguir mayúsculas y cae a 1M", () => {
  assert.equal(parseRange("7d"), "7D");
  assert.equal(parseRange("ALL"), "All");
  assert.equal(parseRange("1y"), "1Y");
  assert.equal(parseRange("1D"), "1M");
  assert.equal(parseRange(null), "1M");
  assert.equal(parseRange(" 3m "), "3M");
});

test("todos los rangos tienen frase", () => {
  for (const r of RANGES) assert.ok(r.phrase.length > 0);
});

test("isValidSlug", () => {
  assert.ok(isValidSlug("bitcoin"));
  assert.ok(isValidSlug("usd-coin"));
  assert.ok(!isValidSlug("../etc"));
  assert.ok(!isValidSlug("Bitcoin"));
  assert.ok(!isValidSlug(""));
  assert.ok(!isValidSlug("a/b"));
});
