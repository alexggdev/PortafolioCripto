import type { TransactionType } from "../../../shared/lib/types";
import { isPortfolioId } from "./portfolioId";

// Lógica pura del formulario de transacciones (sin React): se prueba con tsx.
// Las reglas repiten las del servidor (TransactionValidator) para avisar antes de enviar;
// el servidor siempre vuelve a comprobarlas.

export interface TypeOption {
  value: TransactionType;
  label: string;
  group: "common" | "other";
}

export const TYPE_OPTIONS: readonly TypeOption[] = [
  { value: "Buy", label: "Comprar", group: "common" },
  { value: "Sell", label: "Vender", group: "common" },
  { value: "TransferIn", label: "Transferencia entrante", group: "other" },
  { value: "TransferOut", label: "Transferencia saliente", group: "other" },
];

export function typeLabel(type: TransactionType): string {
  return TYPE_OPTIONS.find((o) => o.value === type)?.label ?? type;
}

/** Compra y venta necesitan precio; las transferencias no lo usan. */
export function priceMode(type: TransactionType): "required" | "none" {
  return type === "Buy" || type === "Sell" ? "required" : "none";
}

export const MAX_NOTE = 200;
const QUANTITY_SCALE = 12;
const PRICE_SCALE = 18;
const FEE_SCALE = 8;
const MAX_INT_DIGITS_QUANTITY = 15;
const MAX_INT_DIGITS_MONEY = 10;
const EARLIEST_UTC_MS = Date.UTC(2009, 0, 3);
const FUTURE_TOLERANCE_MS = 5 * 60_000;

/**
 * Texto escrito por una persona → número decimal normalizado ("1234.5"), o null si no se entiende.
 * Un solo separador (coma o punto) es siempre el decimal: "1,5" y "1.5" valen 1,5.
 * Con los dos a la vez, el último es el decimal y el otro separa miles: "1.234,56" y "1,234.56".
 * Un mismo separador repetido ("1.234.567") separa miles. No admite signo ni notación científica.
 */
export function normalizeDecimal(text: string): string | null {
  const raw = text.trim().replace(/[\s ']/g, "");
  if (!/^[\d.,]+$/.test(raw) || !/\d/.test(raw)) return null;

  const lastDot = raw.lastIndexOf(".");
  const lastComma = raw.lastIndexOf(",");
  let integer: string;
  let fraction = "";

  if (lastDot >= 0 && lastComma >= 0) {
    const decimalAt = Math.max(lastDot, lastComma);
    const thousands = lastDot > lastComma ? "," : ".";
    const left = raw.slice(0, decimalAt);
    if (left.includes(raw[decimalAt])) return null;
    integer = left.split(thousands).join("");
    fraction = raw.slice(decimalAt + 1);
    if (left.split(thousands).slice(1).some((g) => g.length !== 3)) return null;
  } else {
    const sep = lastDot >= 0 ? "." : lastComma >= 0 ? "," : "";
    if (!sep) {
      integer = raw;
    } else {
      const parts = raw.split(sep);
      if (parts.length === 2) {
        integer = parts[0];
        fraction = parts[1];
      } else {
        if (parts.slice(1).some((g) => g.length !== 3) || parts[0].length === 0) return null;
        integer = parts.join("");
      }
    }
  }

  if (!/^\d*$/.test(integer) || !/^\d*$/.test(fraction)) return null;
  integer = integer.replace(/^0+(?=\d)/, "") || "0";
  fraction = fraction.replace(/0+$/, "");
  return fraction ? `${integer}.${fraction}` : integer;
}

function isZero(normalized: string): boolean {
  return normalized === "0";
}

function scaleOf(normalized: string): number {
  const dot = normalized.indexOf(".");
  return dot < 0 ? 0 : normalized.length - dot - 1;
}

function intDigits(normalized: string): number {
  return normalized.split(".")[0].length;
}

export interface TransactionFormValues {
  assetSlug: string;
  type: TransactionType;
  quantity: string;
  price: string;
  fee: string;
  /** Valor de <input type="datetime-local">: AAAA-MM-DDTHH:mm, en la hora local. */
  date: string;
  note: string;
}

export type FieldErrors = Partial<Record<"asset" | "quantity" | "price" | "fee" | "date" | "note", string>>;

/** Hora local escrita en el formulario → ISO en UTC. null si no es una fecha válida. */
export function localToUtcIso(local: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$/.test(local)) return null;
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** ISO en UTC → valor para <input type="datetime-local"> en la hora local. */
export function utcIsoToLocal(iso: string): string {
  const date = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export interface FormResult {
  errors: FieldErrors;
  /** JSON listo para enviar. Los números se escriben tal cual, sin pasar por coma flotante (no pierden decimales). */
  body: string | null;
}

export function validateTransactionForm(
  values: TransactionFormValues,
  nowMs: number,
  portfolioId?: string | null,
): FormResult {
  const errors: FieldErrors = {};

  if (!values.assetSlug.trim()) errors.asset = "Elige una moneda.";

  const quantity = normalizeDecimal(values.quantity);
  if (values.quantity.trim() === "") errors.quantity = "Escribe la cantidad.";
  else if (quantity === null) errors.quantity = "La cantidad no es un número válido.";
  else if (isZero(quantity)) errors.quantity = "La cantidad debe ser mayor que cero.";
  else if (intDigits(quantity) > MAX_INT_DIGITS_QUANTITY) errors.quantity = "La cantidad es demasiado grande.";
  else if (scaleOf(quantity) > QUANTITY_SCALE) errors.quantity = `La cantidad admite como máximo ${QUANTITY_SCALE} decimales.`;

  const mode = priceMode(values.type);
  let price: string | null = null;
  if (mode !== "none") {
    const typed = values.price.trim() !== "";
    price = typed ? normalizeDecimal(values.price) : null;
    if (typed && price === null) errors.price = "El precio no es un número válido.";
    else if (typed && price !== null && intDigits(price) > MAX_INT_DIGITS_MONEY) errors.price = "El precio es demasiado grande.";
    else if (typed && price !== null && scaleOf(price) > PRICE_SCALE) errors.price = `El precio admite como máximo ${PRICE_SCALE} decimales.`;
    else if (mode === "required" && (price === null || isZero(price))) errors.price = "En compras y ventas el precio debe ser mayor que cero.";
  }

  let fee: string | null = null;
  if (values.fee.trim() !== "") {
    fee = normalizeDecimal(values.fee);
    if (fee === null) errors.fee = "La comisión no es un número válido.";
    else if (intDigits(fee) > MAX_INT_DIGITS_MONEY) errors.fee = "La comisión es demasiado grande.";
    else if (scaleOf(fee) > FEE_SCALE) errors.fee = `La comisión admite como máximo ${FEE_SCALE} decimales.`;
  }

  const iso = localToUtcIso(values.date);
  if (iso === null) errors.date = "Elige una fecha y hora válidas.";
  else {
    const ms = Date.parse(iso);
    if (ms < EARLIEST_UTC_MS) errors.date = "La fecha es anterior a la existencia de Bitcoin.";
    else if (ms > nowMs + FUTURE_TOLERANCE_MS) errors.date = "La fecha no puede ser futura.";
  }

  // En una venta, la comisión se descuenta de lo recibido: no puede ser mayor.
  if (!errors.fee && !errors.quantity && !errors.price && values.type === "Sell" && fee !== null && quantity !== null && price !== null) {
    if (Number(fee) > Number(quantity) * Number(price)) errors.fee = "La comisión no puede ser mayor que lo que recibes con la venta.";
  }

  const note = values.note.trim();
  if (note.length > MAX_NOTE) errors.note = `La nota admite como máximo ${MAX_NOTE} caracteres.`;

  if (Object.keys(errors).length > 0 || quantity === null || iso === null) return { errors, body: null };

  const parts = [
    // Solo al crear: en qué portafolio se anota (sin él, la API usa el primero).
    ...(isPortfolioId(portfolioId) ? [`"portfolioId":${JSON.stringify(portfolioId)}`] : []),
    `"assetSlug":${JSON.stringify(values.assetSlug.trim())}`,
    `"type":${JSON.stringify(values.type)}`,
    `"quantity":${quantity}`,
  ];
  if (price !== null) parts.push(`"unitPriceUsd":${price}`);
  if (fee !== null && !isZero(fee)) parts.push(`"feeUsd":${fee}`);
  parts.push(`"dateUtc":${JSON.stringify(iso)}`);
  if (note) parts.push(`"note":${JSON.stringify(note)}`);
  return { errors, body: `{${parts.join(",")}}` };
}
