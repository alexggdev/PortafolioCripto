import type { SortKey } from "../../../shared/lib/types";

export const PAGE_SIZE = 50;
const MAX_PAGE = 1000;
const MAX_SEARCH = 50;

export const SORT_KEYS: readonly SortKey[] = ["rank", "name", "price", "change24h", "marketCap", "volume"];

// Nombres que espera la API de .NET (enum AssetSort).
const API_SORT: Record<SortKey, string> = {
  rank: "Rank",
  name: "Name",
  price: "Price",
  change24h: "Change24h",
  marketCap: "MarketCap",
  volume: "Volume",
};

export interface ListQuery {
  search: string;
  sort: SortKey;
  desc: boolean;
  page: number;
}

export const DEFAULT_QUERY: ListQuery = { search: "", sort: "rank", desc: false, page: 1 };

type RawParams = Record<string, string | string[] | undefined>;

function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

/** Orden natural de cada columna: nombre y ranking de menor a mayor; el resto, de mayor a menor. */
export function defaultDesc(sort: SortKey): boolean {
  return sort !== "rank" && sort !== "name";
}

/** Convierte los parámetros de la URL en una consulta válida. Lo que no se entiende se ignora. */
export function parseListQuery(raw: RawParams): ListQuery {
  const search = (first(raw.search) ?? "").trim().slice(0, MAX_SEARCH);

  const sortRaw = (first(raw.sort) ?? "").toLowerCase();
  const sort = SORT_KEYS.find((k) => k.toLowerCase() === sortRaw) ?? "rank";

  const descRaw = first(raw.desc);
  const desc = descRaw === undefined ? (sortRaw ? defaultDesc(sort) : false) : descRaw === "1" || descRaw === "true";

  const pageNumber = Number.parseInt(first(raw.page) ?? "1", 10);
  const page = Number.isFinite(pageNumber) ? Math.min(Math.max(pageNumber, 1), MAX_PAGE) : 1;

  return { search, sort, desc, page };
}

/** Parámetros de la URL de la web (omite los valores por defecto para que la URL quede limpia). */
export function toSearchParams(query: ListQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.sort !== "rank" || query.desc) {
    params.set("sort", query.sort);
    params.set("desc", query.desc ? "1" : "0");
  }
  if (query.page > 1) params.set("page", String(query.page));
  return params;
}

/** Enlace a la misma lista con algún cambio. Cambiar el orden o la búsqueda vuelve a la página 1. */
export function toPageHref(query: ListQuery, patch: Partial<ListQuery> = {}, basePath = "/"): string {
  const next: ListQuery = { ...query, ...patch };
  const qs = toSearchParams(next).toString();
  return qs ? `${basePath}?${qs}` : basePath;
}

/** Orden que corresponde al hacer clic en una columna: si ya era la activa, se invierte. */
export function nextSort(query: ListQuery, key: SortKey): Pick<ListQuery, "sort" | "desc" | "page"> {
  if (query.sort === key) {
    return { sort: key, desc: !query.desc, page: 1 };
  }
  return { sort: key, desc: defaultDesc(key), page: 1 };
}

/** Parámetros para la API de .NET. */
export function toApiQuery(query: ListQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  params.set("sort", API_SORT[query.sort]);
  params.set("desc", String(query.desc));
  params.set("page", String(query.page));
  params.set("pageSize", String(PAGE_SIZE));
  return params;
}
