import assert from "node:assert/strict";
import { test } from "node:test";
import { safeNextPath } from "../features/auth/lib/safeNext";

test("acepta rutas internas, con su consulta", () => {
  assert.equal(safeNextPath("/portafolio"), "/portafolio");
  assert.equal(safeNextPath("/monedas/bitcoin?range=1Y"), "/monedas/bitcoin?range=1Y");
  assert.equal(safeNextPath("/"), "/");
});

test("rechaza direcciones externas y trucos", () => {
  for (const bad of [
    "https://evil.com",
    "//evil.com",
    "///evil.com",
    "/\\evil.com",
    "javascript:alert(1)",
    "portafolio",
    "/ok\nSet-Cookie: x=1",
    "",
    undefined,
    null,
    42,
  ]) {
    assert.equal(safeNextPath(bad), "/portafolio", String(bad));
  }
});

test("no vuelve al login, al registro ni a rutas de la API (evita bucles)", () => {
  assert.equal(safeNextPath("/login"), "/portafolio");
  assert.equal(safeNextPath("/login?next=/x"), "/portafolio");
  assert.equal(safeNextPath("/registro"), "/portafolio");
  assert.equal(safeNextPath("/api/portfolio"), "/portafolio");
  assert.equal(safeNextPath("/loginx"), "/loginx");
});

test("admite otro valor por defecto", () => {
  assert.equal(safeNextPath("https://x.com", "/"), "/");
});
