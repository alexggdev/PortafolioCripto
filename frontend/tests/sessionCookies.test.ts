import assert from "node:assert/strict";
import { test } from "node:test";
import { cookiesFor, mergeCookieHeader, needsRefresh, parseUtcMs, removeCookies } from "../features/auth/lib/sessionCookies";

const NOW = Date.parse("2026-10-19T10:00:00Z");

test("parseUtcMs entiende fechas de la API con y sin Z", () => {
  assert.equal(parseUtcMs("2026-10-19T10:00:00Z"), NOW);
  assert.equal(parseUtcMs("2026-10-19T10:00:00"), NOW);
  assert.ok(Number.isNaN(parseUtcMs(undefined)));
});

test("cookiesFor con refresh token: JWT corto, refresh y email largos", () => {
  const list = cookiesFor(
    { token: "jwt", expiresAtUtc: "2026-10-19T10:15:00Z", email: "a@b.com", refreshToken: "r", refreshExpiresAtUtc: "2026-11-02T10:00:00Z" },
    NOW + 15 * 60_000,
    NOW,
    true,
  );
  const by = Object.fromEntries(list.map((c) => [c.name, c]));
  assert.equal(by.cp_session.options.maxAge, 900);
  assert.equal(by.cp_refresh.options.maxAge, 14 * 24 * 3600);
  assert.equal(by.cp_email.options.maxAge, 14 * 24 * 3600);
  assert.ok(list.every((c) => c.options.httpOnly && c.options.secure && c.options.sameSite === "lax"));
});

test("cookiesFor sin refresh token se comporta como antes", () => {
  const list = cookiesFor({ token: "jwt", expiresAtUtc: "2026-10-19T11:00:00Z", email: "a@b.com" }, NOW + 3600_000, NOW, false);
  assert.deepEqual(list.map((c) => c.name), ["cp_session", "cp_email"]);
  assert.equal(list[0].options.maxAge, 3600);
});

test("mergeCookieHeader reemplaza, añade y codifica", () => {
  const merged = mergeCookieHeader("a=1; cp_session=old; cp_refresh=oldr", { cp_session: "new", cp_email: "a@b.com" });
  assert.equal(merged, "a=1; cp_refresh=oldr; cp_session=new; cp_email=a%40b.com");
  assert.equal(mergeCookieHeader(null, { x: "1" }), "x=1");
});

test("removeCookies quita solo las indicadas", () => {
  assert.equal(removeCookies("a=1; cp_session=x; b=2", ["cp_session"]), "a=1; b=2");
  assert.equal(removeCookies(undefined, ["cp_session"]), "");
});

test("needsRefresh solo si hay refresh token y el JWT falta o caduca", () => {
  const expired = (t: string) => t === "old";
  assert.equal(needsRefresh(undefined, "r", expired), true);
  assert.equal(needsRefresh("old", "r", expired), true);
  assert.equal(needsRefresh("fresh", "r", expired), false);
  assert.equal(needsRefresh("old", undefined, expired), false);
  assert.equal(needsRefresh(undefined, undefined, expired), false);
});
