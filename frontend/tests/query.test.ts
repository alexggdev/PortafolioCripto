import assert from "node:assert/strict";
import { test } from "node:test";
import {
  DEFAULT_QUERY,
  PAGE_SIZE,
  nextSort,
  parseListQuery,
  toApiQuery,
  toPageHref,
  toSearchParams,
  type ListQuery,
} from "../features/market/lib/query";

test("sin parámetros da la consulta por defecto", () => {
  assert.deepEqual(parseListQuery({}), DEFAULT_QUERY);
});

test("ignora ordenes y páginas inválidos", () => {
  const q = parseListQuery({ sort: "drop table", page: "abc", desc: "quizás" });
  assert.equal(q.sort, "rank");
  assert.equal(q.page, 1);
  assert.equal(q.desc, false);
});

test("acepta el orden sin distinguir mayúsculas y usa el sentido natural si falta desc", () => {
  assert.deepEqual(parseListQuery({ sort: "MARKETCAP" }), { search: "", sort: "marketCap", desc: true, page: 1 });
  assert.equal(parseListQuery({ sort: "name" }).desc, false);
  assert.equal(parseListQuery({ sort: "price", desc: "0" }).desc, false);
  assert.equal(parseListQuery({ sort: "price", desc: "1" }).desc, true);
});

test("acota la página y recorta la búsqueda", () => {
  assert.equal(parseListQuery({ page: "-5" }).page, 1);
  assert.equal(parseListQuery({ page: "0" }).page, 1);
  assert.equal(parseListQuery({ page: "999999" }).page, 1000);
  assert.equal(parseListQuery({ search: "  btc  " }).search, "btc");
  assert.equal(parseListQuery({ search: "x".repeat(200) }).search.length, 50);
});

test("con parámetros repetidos se queda con el primero", () => {
  assert.equal(parseListQuery({ search: ["eth", "btc"] }).search, "eth");
});

test("la URL omite los valores por defecto", () => {
  assert.equal(toSearchParams(DEFAULT_QUERY).toString(), "");
  assert.equal(toPageHref(DEFAULT_QUERY), "/");
  const q: ListQuery = { search: "bit coin", sort: "price", desc: true, page: 3 };
  assert.equal(toSearchParams(q).toString(), "search=bit+coin&sort=price&desc=1&page=3");
  assert.equal(toPageHref(q, { page: 4 }), "/?search=bit+coin&sort=price&desc=1&page=4");
});

test("hacer clic en la columna activa invierte el sentido; en otra, usa el natural y vuelve a la página 1", () => {
  const current: ListQuery = { search: "", sort: "price", desc: true, page: 5 };
  assert.deepEqual(nextSort(current, "price"), { sort: "price", desc: false, page: 1 });
  assert.deepEqual(nextSort(current, "name"), { sort: "name", desc: false, page: 1 });
  assert.deepEqual(nextSort(current, "volume"), { sort: "volume", desc: true, page: 1 });
  assert.deepEqual(nextSort(DEFAULT_QUERY, "rank"), { sort: "rank", desc: true, page: 1 });
});

test("la consulta a la API usa los nombres del enum de .NET y el tamaño de página", () => {
  const params = toApiQuery({ search: "eth", sort: "change24h", desc: true, page: 2 });
  assert.equal(params.get("search"), "eth");
  assert.equal(params.get("sort"), "Change24h");
  assert.equal(params.get("desc"), "true");
  assert.equal(params.get("page"), "2");
  assert.equal(params.get("pageSize"), String(PAGE_SIZE));
  assert.equal(toApiQuery(DEFAULT_QUERY).get("sort"), "Rank");
  assert.equal(toApiQuery(DEFAULT_QUERY).has("search"), false);
});
