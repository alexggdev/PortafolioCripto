import type { RangeCode } from "../../../shared/lib/types";

export interface RangeOption {
  code: RangeCode;
  label: string;
  /** Frase para completar "+4,40 % …". */
  phrase: string;
}

export const RANGES: readonly RangeOption[] = [
  { code: "7D", label: "7 d", phrase: "en 7 días" },
  { code: "1M", label: "1 mes", phrase: "en 1 mes" },
  { code: "3M", label: "3 meses", phrase: "en 3 meses" },
  { code: "1Y", label: "1 año", phrase: "en 1 año" },
  { code: "All", label: "Todo", phrase: "desde el inicio del histórico" },
];

export const DEFAULT_RANGE: RangeCode = "1M";

/** Convierte lo que llegue en la URL en un rango válido (sin distinguir mayúsculas). */
export function parseRange(value: string | null | undefined): RangeCode {
  const wanted = (value ?? "").trim().toLowerCase();
  return RANGES.find((r) => r.code.toLowerCase() === wanted)?.code ?? DEFAULT_RANGE;
}
