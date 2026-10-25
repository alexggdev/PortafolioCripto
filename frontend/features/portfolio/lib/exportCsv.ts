import { txAmountUsd } from "./transactionRows";
import { typeLabel } from "./transactions";
import type { PortfolioSummary, TransactionDto } from "../../../shared/lib/types";

// Archivos de «Exportar»: historial de transacciones y resumen del portafolio, en CSV para Excel (punto y coma,
// coma decimal y BOM UTF-8, para que Excel en español separe bien las columnas y respete las tildes).
// Funciones puras: la zona horaria y la hora actual llegan como argumentos.

export const EXPORT_KINDS = ["history", "overview"] as const;
export type ExportKind = (typeof EXPORT_KINDS)[number];

const SEPARATOR = ";";
const NONE = "--";
const BOM = "﻿";

/** Zona horaria válida de IANA («Europe/Madrid»), o UTC si no lo es. */
export function safeTimeZone(value: string | null | undefined): string {
  if (!value) return "UTC";
  try {
    new Intl.DateTimeFormat("en", { timeZone: value });
    return value;
  } catch {
    return "UTC";
  }
}

function parts(date: Date, timeZone: string): Record<string, number> {
  const out: Record<string, number> = {};
  const formatter = new Intl.DateTimeFormat("en-US", {
    timeZone,
    hourCycle: "h23",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
  for (const part of formatter.formatToParts(date)) if (part.type !== "literal") out[part.type] = Number(part.value);
  return out;
}

const pad = (n: number) => String(n).padStart(2, "0");

/** «2026-10-23 12:32:19» en la zona horaria dada. */
export function formatDateTime(date: Date, timeZone: string): string {
  const p = parts(date, timeZone);
  return `${p.year}-${pad(p.month)}-${pad(p.day)} ${pad(p.hour)}:${pad(p.minute)}:${pad(p.second)}`;
}

/** «UTC+2:00» o «UTC-5:30»: el desfase de la zona en el instante dado. */
export function utcOffsetLabel(date: Date, timeZone: string): string {
  const p = parts(date, timeZone);
  const asUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second);
  const minutes = Math.round((asUtc - Math.floor(date.getTime() / 1000) * 1000) / 60_000);
  const abs = Math.abs(minutes);
  return `UTC${minutes < 0 ? "-" : "+"}${Math.floor(abs / 60)}:${pad(abs % 60)}`;
}

/** Número con coma decimal, sin separador de miles: al menos `min` decimales y como máximo `max`. */
export function plainNumber(value: number, min = 2, max = 8): string {
  if (!Number.isFinite(value)) return NONE;
  let text = value.toFixed(max);
  const [integer, fraction = ""] = text.split(".");
  let trimmed = fraction.replace(/0+$/, "");
  if (trimmed.length < min) trimmed = trimmed.padEnd(min, "0");
  text = trimmed ? `${integer}.${trimmed}` : integer;
  return text.replace(".", ",");
}

const money = (value: number | null | undefined) => (value === null || value === undefined ? NONE : plainNumber(value, 2, 2));
const percent = (value: number | null | undefined, max = 4) => (value === null || value === undefined || !Number.isFinite(value) ? NONE : `${plainNumber(value, 2, max)}%`);

function field(value: string): string {
  return `"${value.replace(/"/g, '""')}"`;
}

function row(values: readonly string[]): string {
  return values.map(field).join(SEPARATOR);
}

/** Las notas que empiezan por = + - @ se tomarían como fórmula al abrirlas en Excel: se les antepone un apóstrofo. */
function safeText(value: string): string {
  return /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
}

/** Historial de transacciones, de la más reciente a la más antigua. */
export function transactionHistoryCsv(transactions: readonly TransactionDto[], timeZone: string, now: Date): string {
  const zone = utcOffsetLabel(now, timeZone);
  const lines = [row([`Fecha (${zone})`, "Activo", "Tipo", "Precio (USD)", "Cantidad", "Valor total (USD)", "Comisión", "Moneda de la comisión", "Notas"])];
  const newestFirst = [...transactions].sort((a, b) => Date.parse(b.dateUtc) - Date.parse(a.dateUtc));

  for (const tx of newestFirst) {
    const amount = txAmountUsd(tx);
    lines.push(
      row([
        formatDateTime(new Date(tx.dateUtc), timeZone),
        tx.symbol.toUpperCase(),
        typeLabel(tx.type),
        tx.unitPriceUsd > 0 ? plainNumber(tx.unitPriceUsd, 2, 8) : NONE,
        plainNumber(tx.quantity, 2, 8),
        amount === null ? NONE : plainNumber(amount, 2, 2),
        tx.feeUsd > 0 ? plainNumber(tx.feeUsd, 2, 8) : NONE,
        tx.feeUsd > 0 ? "USD" : "",
        tx.note ? safeText(tx.note) : "",
      ]),
    );
  }
  return BOM + lines.join("\r\n") + "\r\n";
}

/** Resumen del portafolio: totales arriba y una fila por moneda, de mayor a menor valor. */
export function portfolioOverviewCsv(summary: PortfolioSummary, timeZone: string, now: Date): string {
  const zone = utcOffsetLabel(now, timeZone);
  const totalPnlPct = summary.costBasisUsd > 0 ? (summary.totalPnlUsd / summary.costBasisUsd) * 100 : null;
  const lines = [
    row([`Última actualización (${zone})`, formatDateTime(now, timeZone)]),
    row(["Moneda", "USD"]),
    row(["Valor total (USD)", money(summary.valueUsd)]),
    row(["Cambio del valor total en 24 h (USD)", money(summary.change24hUsd)]),
    row(["Cambio del valor total en 24 h %", percent(summary.change24hPct, 2)]),
    row(["Beneficio histórico (USD)", money(summary.totalPnlUsd)]),
    row(["Beneficio histórico %", percent(totalPnlPct, 2)]),
    "",
    "Activos",
    row(["Nombre", "Precio (USD)", "1h %", "24h %", "7d %", "Valor (USD)", "Cantidad", "Precio promedio de compra (USD)", "Ganancia / Pérdida (USD)", "Ganancia / Pérdida %"]),
  ];

  const byValue = [...summary.positions].sort((a, b) => (b.valueUsd ?? -1) - (a.valueUsd ?? -1));
  for (const p of byValue) {
    lines.push(
      row([
        safeText(p.name),
        p.priceUsd === null ? NONE : plainNumber(p.priceUsd, 2, 8),
        percent(p.change1hPct),
        percent(p.change24hPct),
        percent(p.change7dPct),
        money(p.valueUsd),
        plainNumber(p.quantity, 2, 8),
        plainNumber(p.averageCostUsd, 2, 8),
        money(p.unrealizedPnlUsd),
        percent(p.unrealizedPnlPct, 2),
      ]),
    );
  }
  return BOM + lines.join("\r\n") + "\r\n";
}

/** «Portfolio_2» a partir del nombre del portafolio: solo letras, números, guiones y guiones bajos. */
export function exportBaseName(portfolioName: string | null | undefined): string {
  const slug = (portfolioName ?? "")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^A-Za-z0-9_-]+/g, "_")
    .replace(/^_+|_+$/g, "");
  return slug || "Portfolio";
}

export function exportFileName(base: string, kinds: readonly ExportKind[]): string {
  if (kinds.length > 1) return `${base}_for_all.zip`;
  return `${base}_with_${kinds[0] === "history" ? "transaction_history" : "portfolio_overview"}.csv`;
}

/** Los tipos pedidos en la URL («history,overview»), sin repetidos y en orden fijo; vacío si no hay ninguno válido. */
export function parseKinds(value: string | null | undefined): ExportKind[] {
  const wanted = new Set((value ?? "").split(","));
  return EXPORT_KINDS.filter((kind) => wanted.has(kind));
}
