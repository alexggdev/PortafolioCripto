// El id de un portafolio es un GUID. Se valida antes de usarlo en una URL hacia la API.

const GUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function isPortfolioId(value: unknown): value is string {
  return typeof value === "string" && GUID.test(value);
}

/** "&portfolioId=…" si el id es válido; si no, cadena vacía (la API usará el primer portafolio). */
export function portfolioQuery(id: string | null | undefined): string {
  return isPortfolioId(id) ? `&portfolioId=${id}` : "";
}
