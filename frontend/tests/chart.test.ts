import assert from "node:assert/strict";
import { test } from "node:test";
import {
  areaPath,
  compactAxisLabels,
  formatAxisNumber,
  formatLongDate,
  formatPctAxis,
  formatShortDate,
  formatTickDate,
  linePath,
  nearestIndex,
  niceTicks,
  paddedDomain,
  pickDateTicks,
  scaleLinear,
} from "../shared/lib/chart";

const DAY = 86_400_000;

test("niceTicks da valores redondos dentro del rango", () => {
  const { values, step } = niceTicks(80_123, 86_877, 5);
  assert.equal(step, 1000);
  assert.deepEqual(values, [81_000, 82_000, 83_000, 84_000, 85_000, 86_000]);
  assert.ok(values.every((v) => v >= 80_123 && v <= 86_877));
});

test("niceTicks funciona con precios minúsculos sin ruido de coma flotante", () => {
  const { values, step } = niceTicks(0.0000118, 0.0000126, 4);
  assert.ok(step < 0.000001);
  for (const v of values) {
    assert.equal(String(v).includes("0000000"), false, `ruido en ${v}`);
  }
  assert.ok(values.length >= 2);
});

test("niceTicks con rango vacío devuelve un solo valor", () => {
  assert.deepEqual(niceTicks(5, 5).values, [5]);
  assert.deepEqual(niceTicks(Number.NaN, 5).values, []);
});

test("paddedDomain deja aire y nunca baja de cero", () => {
  const [lo, hi] = paddedDomain([100, 110]);
  assert.ok(lo < 100 && hi > 110);
  assert.deepEqual(paddedDomain([0.5, 100]).map((v) => v >= 0), [true, true]);
  assert.equal(paddedDomain([0, 10])[0], 0);
  const [flatLo, flatHi] = paddedDomain([50, 50]);
  assert.ok(flatLo < 50 && flatHi > 50);
  assert.deepEqual(paddedDomain([]), [0, 1]);
});

test("scaleLinear mapea el dominio al rango (también invertido) y no divide por cero", () => {
  const y = scaleLinear([0, 100], [200, 0]);
  assert.equal(y(0), 200);
  assert.equal(y(100), 0);
  assert.equal(y(25), 150);
  assert.equal(scaleLinear([5, 5], [0, 10])(5), 5);
});

test("linePath y areaPath", () => {
  assert.equal(linePath([0, 10], [5, 15]), "M0.0,5.0 L10.0,15.0");
  assert.equal(areaPath([0, 10], [5, 15], 100), "M0.0,5.0 L10.0,15.0 L10.0,100.0 L0.0,100.0 Z");
  assert.equal(areaPath([], [], 100), "");
});

test("nearestIndex encuentra el punto más cercano", () => {
  const xs = [0, 10, 20, 30];
  assert.equal(nearestIndex(xs, -5), 0);
  assert.equal(nearestIndex(xs, 4), 0);
  assert.equal(nearestIndex(xs, 6), 1);
  assert.equal(nearestIndex(xs, 29), 3);
  assert.equal(nearestIndex(xs, 500), 3);
  assert.equal(nearestIndex([], 1), -1);
});

test("las fechas del eje se adaptan a lo larga que es la serie", () => {
  const t = Date.UTC(2026, 9, 12);
  assert.equal(formatTickDate(t, 30 * DAY), "12 oct");
  assert.equal(formatTickDate(t, 365 * DAY), "oct 2026");
  assert.equal(formatTickDate(t, 2000 * DAY), "2026");
  assert.equal(formatLongDate(t), "12 de octubre de 2026");
});

test("pickDateTicks reparte por igual e incluye los extremos", () => {
  assert.deepEqual(pickDateTicks(0, 400, 5), [0, 100, 200, 300, 400]);
  assert.deepEqual(pickDateTicks(7, 7, 5), [7]);
});

test("formatAxisNumber elige decimales según el paso", () => {
  assert.equal(formatAxisNumber(84_000, 2000), "84.000");
  assert.equal(formatAxisNumber(1850, 50), "1.850");
  assert.equal(formatAxisNumber(0.65, 0.05), "0,65");
  assert.equal(formatAxisNumber(0.0000125, 0.0000005), "0,0000125");
});

test("formatShortDate escribe día, mes corto y año en UTC", () => {
  assert.match(formatShortDate(Date.UTC(2026, 9, 20)), /^20 oct\.? 2026$/);
});

test("compactAxisLabels añade decimales hasta que las etiquetas dejan de repetirse", () => {
  const labels = compactAxisLabels([4_190_000, 4_195_000, 4_200_000, 4_205_000], "USD");
  assert.equal(new Set(labels).size, labels.length);
  assert.ok(labels.every((l) => l.startsWith("$")));
  assert.deepEqual(compactAxisLabels([10_400, 10_600, 10_800], "USD"), ["$10.4K", "$10.6K", "$10.8K"]);
});

test("formatPctAxis compacta los miles y conserva el signo", () => {
  assert.equal(formatPctAxis(0), "0%");
  assert.equal(formatPctAxis(300), "300%");
  assert.equal(formatPctAxis(1500), "1.5K%");
  assert.equal(formatPctAxis(-300), "-300%");
  assert.equal(formatPctAxis(2000), "2K%");
});
