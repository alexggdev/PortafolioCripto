import assert from "node:assert/strict";
import { test } from "node:test";
import { exportHref } from "../features/portfolio/lib/exportUrl";

const ID = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

test("la URL lleva lo elegido, la zona horaria y el portafolio", () => {
  const url = new URL(exportHref(ID, ["history", "overview"], "Europe/Madrid"), "http://x");
  assert.equal(url.pathname, "/api/portfolio/export");
  assert.equal(url.searchParams.get("include"), "history,overview");
  assert.equal(url.searchParams.get("tz"), "Europe/Madrid");
  assert.equal(url.searchParams.get("portfolioId"), ID);
});

test("un id que no es un GUID no se envía", () => {
  const url = new URL(exportHref("../admin", ["overview"], "UTC"), "http://x");
  assert.equal(url.searchParams.has("portfolioId"), false);
  assert.equal(exportHref(null, ["overview"], "UTC").includes("portfolioId"), false);
});
