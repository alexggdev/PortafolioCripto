import assert from "node:assert/strict";
import { test } from "node:test";
import { parseMoveBody } from "../features/portfolio/lib/moveBody";

const A = "11111111-1111-1111-1111-111111111111";
const B = "22222222-2222-2222-2222-222222222222";

test("parseMoveBody acepta dos ids distintos y descarta lo demás", () => {
  assert.deepEqual(parseMoveBody(JSON.stringify({ fromPortfolioId: A, toPortfolioId: B, extra: 1 })), {
    fromPortfolioId: A,
    toPortfolioId: B,
  });
});

test("parseMoveBody rechaza ids repetidos, inválidos o ausentes", () => {
  assert.equal(parseMoveBody(JSON.stringify({ fromPortfolioId: A, toPortfolioId: A })), null);
  assert.equal(parseMoveBody(JSON.stringify({ fromPortfolioId: A, toPortfolioId: "no-es-guid" })), null);
  assert.equal(parseMoveBody(JSON.stringify({ fromPortfolioId: A })), null);
  assert.equal(parseMoveBody("null"), null);
  assert.equal(parseMoveBody("{roto"), null);
});
