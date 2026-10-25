import { redirectToLogin } from "../../auth/lib/clientAuth";
import type { AssetSummary, PagedResult } from "../../../shared/lib/types";

const SHOWN = 20;
const TOP_TTL_MS = 5 * 60_000;

// Las primeras monedas del ranking se piden antes de abrir la ventana y se recuerdan unos minutos:
// así las listas de monedas se ven al instante.
let top: { items: AssetSummary[]; at: number } | null = null;
let pending: Promise<AssetSummary[]> | null = null;

/** La lista inicial si se pidió hace poco; si no, null. */
export function cachedTopCoins(): AssetSummary[] | null {
  return top && Date.now() - top.at < TOP_TTL_MS ? top.items : null;
}

export async function searchCoins(query: string, signal?: AbortSignal): Promise<AssetSummary[]> {
  const params = new URLSearchParams();
  if (query) params.set("search", query.slice(0, 50));
  const response = await fetch(`/api/assets?${params}`, { signal, cache: "no-store" });
  if (response.status === 401) redirectToLogin();
  if (!response.ok) throw new Error(String(response.status));
  return ((await response.json()) as PagedResult<AssetSummary>).items.slice(0, SHOWN);
}

/** Pide por adelantado la lista inicial (no hace nada si ya la tiene o ya la está pidiendo). */
export function prefetchTopCoins(): Promise<AssetSummary[]> {
  const cached = cachedTopCoins();
  if (cached) return Promise.resolve(cached);
  pending ??= searchCoins("")
    .then((items) => {
      top = { items, at: Date.now() };
      return items;
    })
    .finally(() => {
      pending = null;
    });
  return pending;
}
