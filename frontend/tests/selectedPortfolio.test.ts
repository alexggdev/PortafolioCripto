import assert from "node:assert/strict";
import { test } from "node:test";
import { pickPortfolio, selectedId } from "../features/portfolio/lib/selectedPortfolio";

const A = "eacd6bda-12f1-499c-83a1-5ffd70a86afe";
const B = "148503de-676a-4dd9-839d-e6c7ce16a0d7";

test("selectedId solo acepta un GUID", () => {
  assert.equal(selectedId(A), A);
  assert.equal(selectedId("../../etc"), null);
  assert.equal(selectedId(undefined), null);
});

test("pickPortfolio prefiere el elegido, luego el predeterminado y si no hay ninguno devuelve null", () => {
  const list = [{ id: A, isDefault: false }, { id: B, isDefault: true }];
  assert.equal(pickPortfolio(list, A)?.id, A);
  assert.equal(pickPortfolio(list, null)?.id, B);
  assert.equal(pickPortfolio(list, "otro")?.id, B);
  assert.equal(pickPortfolio([{ id: A, isDefault: false }], null), null);
});

test("parseScreen: portafolio por defecto, resumen y moneda con slug válido", async () => {
  const { parseScreen, screenCookie } = await import("../features/portfolio/lib/selectedPortfolio");
  assert.deepEqual(parseScreen(undefined), { kind: "portafolio" });
  assert.deepEqual(parseScreen("resumen"), { kind: "resumen" });
  assert.deepEqual(parseScreen("moneda:bitcoin"), { kind: "moneda", slug: "bitcoin" });
  assert.deepEqual(parseScreen("moneda:../x"), { kind: "portafolio" });
  assert.deepEqual(parseScreen("cualquier cosa"), { kind: "portafolio" });
  assert.equal(screenCookie({ kind: "moneda", slug: "eth" }), "moneda:eth");
  assert.equal(screenCookie({ kind: "portafolio" }), null);
});
