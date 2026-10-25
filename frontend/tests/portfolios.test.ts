import assert from "node:assert/strict";
import { test } from "node:test";
import { copyName, dropIndex, MAX_NAME, moveItem, pickDefault, totalOfIncluded } from "../features/portfolio/lib/portfolios";

test("pickDefault usa el marcado y, si no hay, el primero", () => {
  assert.equal(pickDefault([{ isDefault: false, n: 1 }, { isDefault: true, n: 2 }])?.n, 2);
  assert.equal(pickDefault([{ isDefault: false, n: 1 }, { isDefault: false, n: 2 }])?.n, 1);
  assert.equal(pickDefault([]), null);
});

test("moveItem recoloca un elemento sin tocar el original", () => {
  const list = ["a", "b", "c", "d"];
  assert.deepEqual(moveItem(list, 0, 2), ["b", "c", "a", "d"]);
  assert.deepEqual(moveItem(list, 3, 1), ["a", "d", "b", "c"]);
  assert.deepEqual(list, ["a", "b", "c", "d"]);
});

test("moveItem ignora posiciones que no existen", () => {
  assert.deepEqual(moveItem(["a", "b"], 0, 5), ["a", "b"]);
  assert.deepEqual(moveItem(["a", "b"], -1, 0), ["a", "b"]);
  assert.deepEqual(moveItem(["a", "b"], 1, 1), ["a", "b"]);
});

test("copyName añade «Copia» y respeta el máximo", () => {
  assert.equal(copyName("Largo plazo"), "Largo plazo Copia");
  const long = copyName("x".repeat(MAX_NAME));
  assert.equal(long.length, MAX_NAME);
  assert.ok(long.endsWith(" Copia"));
});

test("totalOfIncluded no suma los portafolios excluidos", () => {
  assert.equal(
    totalOfIncluded([
      { valueUsd: 100, includeInTotal: true },
      { valueUsd: 50, includeInTotal: false },
      { valueUsd: 7, includeInTotal: true },
    ]),
    107,
  );
  assert.equal(totalOfIncluded([]), 0);
});

test("dropIndex redondea a la fila más cercana y no se sale de la lista", () => {
  assert.equal(dropIndex(1, 0, 50, 4), 1);
  assert.equal(dropIndex(1, 80, 50, 4), 3);
  assert.equal(dropIndex(1, 20, 50, 4), 1);
  assert.equal(dropIndex(1, -300, 50, 4), 0);
  assert.equal(dropIndex(2, 900, 50, 4), 3);
  assert.equal(dropIndex(2, 40, 0, 4), 2);
});
