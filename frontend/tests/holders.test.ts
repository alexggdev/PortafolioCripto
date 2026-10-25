import assert from "node:assert/strict";
import { test } from "node:test";
import { holderLines } from "../features/portfolio/lib/holders";

test("holderLines valora cada portafolio al precio actual", () => {
  const lines = holderLines([{ portfolioId: "a", quantity: 2 }, { portfolioId: "b", quantity: 0.5 }], 100);
  assert.deepEqual(lines, [
    { portfolioId: "a", quantity: 2, valueUsd: 200 },
    { portfolioId: "b", quantity: 0.5, valueUsd: 50 },
  ]);
});

test("holderLines sin precio deja el valor vacío y sin datos devuelve lista vacía", () => {
  assert.equal(holderLines([{ portfolioId: "a", quantity: 1 }], null)[0].valueUsd, null);
  assert.deepEqual(holderLines(undefined, 5), []);
});
