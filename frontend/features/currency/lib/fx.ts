import { apiGet } from "../../../shared/lib/api";
import { sanitizeRates, USD_ONLY, type FxRates } from "./currency";

/**
 * Tipos de cambio desde la API de .NET (que los lee de su caché en memoria). Es solo presentación:
 * si falla o tarda, la web sigue funcionando en dólares en vez de romperse.
 */
export async function loadRates(): Promise<FxRates> {
  try {
    return sanitizeRates(await apiGet<unknown>("/api/fx", { timeoutMs: 2500, retry: false }));
  } catch {
    return USD_ONLY;
  }
}
