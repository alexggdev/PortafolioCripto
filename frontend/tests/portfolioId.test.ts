import assert from "node:assert/strict";
import { test } from "node:test";
import { isPortfolioId, portfolioQuery } from "../features/portfolio/lib/portfolioId";

const ID = "eacd6bda-12f1-499c-83a1-5ffd70a86afe";

test("isPortfolioId solo acepta GUID", () => {
  assert.equal(isPortfolioId(ID), true);
  assert.equal(isPortfolioId("123"), false);
  assert.equal(isPortfolioId("../../x"), false);
  assert.equal(isPortfolioId(undefined), false);
});

test("portfolioQuery ignora ids inválidos", () => {
  assert.equal(portfolioQuery(ID), `&portfolioId=${ID}`);
  assert.equal(portfolioQuery("no-es-un-guid"), "");
  assert.equal(portfolioQuery(null), "");
});
