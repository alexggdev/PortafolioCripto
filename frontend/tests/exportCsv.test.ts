import assert from "node:assert/strict";
import { test } from "node:test";
import { exportBaseName, exportFileName, formatDateTime, parseKinds, plainNumber, portfolioOverviewCsv, safeTimeZone, transactionHistoryCsv, utcOffsetLabel } from "../features/portfolio/lib/exportCsv";
import type { PortfolioSummary, PositionDto, TransactionDto } from "../shared/lib/types";

const NOW = new Date("2026-10-23T10:32:19Z");
const MADRID = "Europe/Madrid";

function tx(over: Partial<TransactionDto>): TransactionDto {
  return {
    id: 1,
    assetSlug: "bnb",
    symbol: "bnb",
    name: "BNB",
    imageUrl: null,
    type: "Buy",
    quantity: 2,
    unitPriceUsd: 745.27,
    feeUsd: 0,
    dateUtc: "2026-10-23T10:00:00Z",
    note: null,
    ...over,
  };
}

function position(over: Partial<PositionDto>): PositionDto {
  return {
    assetId: 1,
    slug: "bnb",
    symbol: "BNB",
    name: "BNB",
    imageUrl: null,
    quantity: 22,
    averageCostUsd: 274.9,
    costBasisUsd: 6047.8,
    priceUsd: 747.14,
    priceSource: "live",
    priceUpdatedAtUtc: null,
    valueUsd: 16437.15,
    unrealizedPnlUsd: 10389.41,
    unrealizedPnlPct: 171.79,
    realizedPnlUsd: 0,
    allocationPct: 14.6,
    change24hPct: 1.7415,
    change1hPct: 0.47,
    change7dPct: 2.3321,
    ...over,
  };
}

test("fecha y desfase en la zona del usuario", () => {
  assert.equal(formatDateTime(NOW, MADRID), "2026-10-23 12:32:19");
  assert.equal(utcOffsetLabel(NOW, MADRID), "UTC+2:00");
  assert.equal(utcOffsetLabel(new Date("2026-01-15T10:00:00Z"), MADRID), "UTC+1:00");
  assert.equal(utcOffsetLabel(NOW, "UTC"), "UTC+0:00");
  assert.equal(utcOffsetLabel(NOW, "Asia/Kolkata"), "UTC+5:30");
  assert.equal(utcOffsetLabel(NOW, "America/Lima"), "UTC-5:00");
});

test("una zona horaria inventada se sustituye por UTC", () => {
  assert.equal(safeTimeZone("Mars/Olympus"), "UTC");
  assert.equal(safeTimeZone(null), "UTC");
  assert.equal(safeTimeZone(MADRID), MADRID);
});

test("números con coma decimal y sin separador de miles", () => {
  assert.equal(plainNumber(112112.02, 2, 2), "112112,02");
  assert.equal(plainNumber(22), "22,00");
  assert.equal(plainNumber(0.12345678), "0,12345678");
  assert.equal(plainNumber(1.7757), "1,7757");
  assert.equal(plainNumber(93803.84000000001, 2, 2), "93803,84");
});

test("historial: cabecera, orden de más reciente a más antigua y valores", () => {
  const csv = transactionHistoryCsv(
    [
      tx({ id: 1, dateUtc: "2023-03-14T11:05:00Z", symbol: "hype", type: "Buy", quantity: 1000, unitPriceUsd: 4, note: "basura" }),
      tx({ id: 2 }),
      tx({ id: 3, dateUtc: "2025-10-24T01:45:00Z", symbol: "atom", type: "Sell", quantity: 2, unitPriceUsd: 1.9636, feeUsd: 0.5 }),
    ],
    MADRID,
    NOW,
  );
  const lines = csv.replace("﻿", "").trim().split("\r\n");
  assert.equal(lines[0], '"Fecha (UTC+2:00)";"Activo";"Tipo";"Precio (USD)";"Cantidad";"Valor total (USD)";"Comisión";"Moneda de la comisión";"Notas"');
  assert.equal(lines[1], '"2026-10-23 12:00:00";"BNB";"Comprar";"745,27";"2,00";"1490,54";"--";"";""');
  assert.equal(lines[2], '"2025-10-24 03:45:00";"ATOM";"Vender";"1,9636";"2,00";"3,93";"0,50";"USD";""');
  assert.equal(lines[3], '"2023-03-14 12:05:00";"HYPE";"Comprar";"4,00";"1000,00";"4000,00";"--";"";"basura"');
  assert.ok(csv.startsWith("﻿"));
});

test("las notas que empiezan como fórmula no se ejecutan en Excel y las comillas se duplican", () => {
  const csv = transactionHistoryCsv([tx({ note: '=1+1 "x"' })], "UTC", NOW);
  assert.ok(csv.includes(`"'=1+1 ""x"""`));
});

test("una transacción sin precio no inventa un total", () => {
  const csv = transactionHistoryCsv([tx({ type: "TransferIn", unitPriceUsd: 0 })], "UTC", NOW);
  assert.ok(csv.includes('"--";"2,00";"--";"--"'));
});

test("resumen: totales, tabla ordenada por valor y porcentaje de beneficio sobre la base de costo", () => {
  const summary = {
    name: "Portfolio 2",
    valueUsd: 112112.02,
    costBasisUsd: 18308.18,
    totalPnlUsd: 93803.84,
    change24hUsd: 1102.49,
    change24hPct: 0.99,
    positions: [
      position({ name: "Cosmos", valueUsd: 100.83, unrealizedPnlUsd: -410.16, unrealizedPnlPct: -79.66, priceUsd: 1.7757, quantity: 56.78 }),
      position({}),
    ],
  } as unknown as PortfolioSummary;
  const lines = portfolioOverviewCsv(summary, MADRID, NOW).replace("﻿", "").trim().split("\r\n");
  assert.equal(lines[0], '"Última actualización (UTC+2:00)";"2026-10-23 12:32:19"');
  assert.equal(lines[2], '"Valor total (USD)";"112112,02"');
  assert.equal(lines[4], '"Cambio del valor total en 24 h %";"0,99%"');
  assert.equal(lines[5], '"Beneficio histórico (USD)";"93803,84"');
  assert.equal(lines[6], '"Beneficio histórico %";"512,36%"');
  assert.equal(lines[8], "Activos");
  assert.equal(lines[10], '"BNB";"747,14";"0,47%";"1,7415%";"2,3321%";"16437,15";"22,00";"274,90";"10389,41";"171,79%"');
  assert.ok(lines[11].startsWith('"Cosmos";"1,7757"'));
  assert.ok(lines[11].endsWith('"-410,16";"-79,66%"'));
});

test("nombres de archivo: uno suelto o el ZIP con los dos", () => {
  assert.equal(exportBaseName("Portfolio 2"), "Portfolio_2");
  assert.equal(exportBaseName("Largo plazo ñandú / 1"), "Largo_plazo_nandu_1");
  assert.equal(exportBaseName("***"), "Portfolio");
  assert.equal(exportFileName("Portfolio_2", ["history"]), "Portfolio_2_with_transaction_history.csv");
  assert.equal(exportFileName("Portfolio_2", ["overview"]), "Portfolio_2_with_portfolio_overview.csv");
  assert.equal(exportFileName("Portfolio_2", ["history", "overview"]), "Portfolio_2_for_all.zip");
});

test("los tipos pedidos se filtran y se ordenan", () => {
  assert.deepEqual(parseKinds("overview,history,history,x"), ["history", "overview"]);
  assert.deepEqual(parseKinds("x"), []);
  assert.deepEqual(parseKinds(null), []);
});
