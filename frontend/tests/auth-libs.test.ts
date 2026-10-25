import assert from "node:assert/strict";
import { test } from "node:test";
import { extractErrors } from "../shared/lib/apiErrors";
import { isTokenExpired, jwtExpiresAtMs } from "../features/auth/lib/jwt";

function makeJwt(payload: object): string {
  const b64 = (o: object) => Buffer.from(JSON.stringify(o)).toString("base64url");
  return `${b64({ alg: "HS256" })}.${b64(payload)}.firma`;
}

test("lee la caducidad del JWT", () => {
  assert.equal(jwtExpiresAtMs(makeJwt({ exp: 1_800_000_000 })), 1_800_000_000_000);
});

test("un token ilegible o sin exp cuenta como caducado", () => {
  assert.equal(jwtExpiresAtMs("basura"), null);
  assert.equal(jwtExpiresAtMs("a.b.c"), null);
  assert.equal(jwtExpiresAtMs(makeJwt({ sub: "x" })), null);
  assert.equal(isTokenExpired("basura", 0), true);
});

test("caducado con margen de 30 s", () => {
  const token = makeJwt({ exp: 1000 });
  assert.equal(isTokenExpired(token, 900_000), false);
  assert.equal(isTokenExpired(token, 975_000), true);
  assert.equal(isTokenExpired(token, 1_100_000), true);
});

test("extrae errores: lista de textos, validación de ASP.NET, detail y respaldo", () => {
  assert.deepEqual(extractErrors({ errors: ["Uno.", "Dos."], detail: "Uno. Dos." }, "x"), ["Uno.", "Dos."]);
  assert.deepEqual(extractErrors({ errors: { Email: ["El email no es válido."], Password: ["Corta."] } }, "x"), [
    "El email no es válido.",
    "Corta.",
  ]);
  assert.deepEqual(extractErrors({ detail: "Solo detalle." }, "x"), ["Solo detalle."]);
  assert.deepEqual(extractErrors({ title: "Título" }, "x"), ["Título"]);
  assert.deepEqual(extractErrors(null, "respaldo"), ["respaldo"]);
  assert.deepEqual(extractErrors({}, "respaldo"), ["respaldo"]);
});
