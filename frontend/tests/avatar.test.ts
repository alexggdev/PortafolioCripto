import assert from "node:assert/strict";
import { test } from "node:test";
import { AVATAR_COUNT, avatarCells, AVATAR_GRID, avatarIndex } from "../features/portfolio/lib/avatar";

test("cada imagen tiene 12×12 celdas de colores hexadecimales", () => {
  for (let i = 0; i < AVATAR_COUNT; i++) {
    const cells = avatarCells(i);
    assert.equal(cells.length, AVATAR_GRID * AVATAR_GRID);
    for (const cell of cells) assert.match(cell, /^#[0-9a-f]{6}$/);
  }
});

test("las 28 imágenes son distintas entre sí y siempre iguales", () => {
  const all = Array.from({ length: AVATAR_COUNT }, (_, i) => avatarCells(i).join());
  assert.equal(new Set(all).size, AVATAR_COUNT);
  assert.deepEqual(avatarCells(5), avatarCells(5));
});

test("un número fuera de rango da la vuelta en vez de fallar", () => {
  assert.deepEqual(avatarCells(AVATAR_COUNT + 3), avatarCells(3));
  assert.deepEqual(avatarCells(-1), avatarCells(AVATAR_COUNT - 1));
});

test("sin imagen elegida se usa una según el id, siempre la misma", () => {
  const id = "11111111-1111-1111-1111-111111111111";
  const index = avatarIndex({ id, avatar: null });
  assert.ok(index >= 0 && index < AVATAR_COUNT);
  assert.equal(avatarIndex({ id, avatar: null }), index);
});

test("la imagen elegida manda; si no es válida, se usa la del id", () => {
  const id = "22222222-2222-2222-2222-222222222222";
  assert.equal(avatarIndex({ id, avatar: 7 }), 7);
  assert.equal(avatarIndex({ id, avatar: 99 }), avatarIndex({ id, avatar: null }));
  assert.equal(avatarIndex({ id, avatar: -2 }), avatarIndex({ id, avatar: null }));
});
