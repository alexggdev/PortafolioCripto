import assert from "node:assert/strict";
import { test } from "node:test";
import { formatLocal, monthGrid, parseLocal, pickDay, timeSlots } from "../features/portfolio/lib/calendar";

test("monthGrid tiene siempre 6 semanas y empieza en lunes", () => {
  const grid = monthGrid(2023, 9); // octubre de 2023: el día 1 fue domingo
  assert.equal(grid.length, 6);
  assert.ok(grid.every((week) => week.length === 7));
  assert.deepEqual({ day: grid[0][6].day, inMonth: grid[0][6].inMonth }, { day: 1, inMonth: true });
  assert.equal(grid[0][0].inMonth, false); // 25 de septiembre
  assert.equal(grid[0][0].day, 25);
});

test("monthGrid marca los días del mes siguiente como fuera del mes", () => {
  const last = monthGrid(2023, 9)[5];
  assert.ok(last.every((c) => !c.inMonth || c.day <= 31));
  assert.equal(last[6].month, 10);
});

test("parseLocal y formatLocal son inversas", () => {
  const parts = parseLocal("2026-03-05T09:07")!;
  assert.deepEqual(parts, { year: 2026, month: 2, day: 5, hour: 9, minute: 7 });
  assert.equal(formatLocal(parts.year, parts.month, parts.day, parts.hour, parts.minute), "2026-03-05T09:07");
  assert.equal(parseLocal("hola"), null);
});

test("timeSlots reparte el día cada 15 minutos", () => {
  const slots = timeSlots();
  assert.equal(slots.length, 96);
  assert.equal(slots[0], "00:00");
  assert.equal(slots[95], "23:45");
});

test("pickDay conserva la hora y no pasa del límite", () => {
  assert.equal(pickDay("2026-10-01T12:34", { year: 2026, month: 9, day: 20 }, "2026-10-21T22:00"), "2026-10-20T12:34");
  assert.equal(pickDay("2026-10-01T23:50", { year: 2026, month: 9, day: 21 }, "2026-10-21T22:00"), "2026-10-21T22:00");
});
