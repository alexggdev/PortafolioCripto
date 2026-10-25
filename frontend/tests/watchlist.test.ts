import assert from "node:assert/strict";
import { test } from "node:test";
import { setWatched } from "../features/watchlist/lib/watchlist";

const reply = (status: number, body: unknown = null) => async () => ({
  ok: status >= 200 && status < 300,
  status,
  json: async () => body,
});

test("setWatched usa PUT para seguir y DELETE para dejar de seguir", async () => {
  const calls: { url: string; method?: string }[] = [];
  const spy = async (url: string, init: RequestInit) => {
    calls.push({ url, method: init.method });
    return { ok: true, status: 204, json: async () => null };
  };
  assert.equal((await setWatched("bitcoin", true, spy)).ok, true);
  assert.equal((await setWatched("usd-coin", false, spy)).ok, true);
  assert.deepEqual(calls, [
    { url: "/api/watchlist/bitcoin", method: "PUT" },
    { url: "/api/watchlist/usd-coin", method: "DELETE" },
  ]);
});

test("setWatched devuelve los mensajes de la API y el estado", async () => {
  const r = await setWatched("x", true, reply(400, { errors: ["Solo puedes seguir hasta 100 monedas."] }));
  assert.equal(r.ok, false);
  assert.equal(r.status, 400);
  assert.deepEqual(r.errors, ["Solo puedes seguir hasta 100 monedas."]);
});

test("setWatched conserva el 401 y usa un mensaje por defecto si el cuerpo no sirve", async () => {
  const r = await setWatched("x", true, async () => ({ ok: false, status: 401, json: async () => { throw new Error("no json"); } }));
  assert.equal(r.status, 401);
  assert.equal(r.errors.length, 1);
});

test("setWatched no lanza si falla la red", async () => {
  const r = await setWatched("x", false, async () => { throw new Error("offline"); });
  assert.equal(r.ok, false);
  assert.equal(r.status, 0);
  assert.match(r.errors[0], /conectar/);
});

test("setWatched codifica el slug", async () => {
  let seen = "";
  await setWatched("a/b", true, async (url) => { seen = url; return { ok: true, status: 204, json: async () => null }; });
  assert.equal(seen, "/api/watchlist/a%2Fb");
});
