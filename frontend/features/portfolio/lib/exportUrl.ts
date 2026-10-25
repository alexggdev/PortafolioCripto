import { isPortfolioId } from "./portfolioId";
import type { ExportKind } from "./exportCsv";

/** Enlace de descarga de «Exportar» (sin id de portafolio, la API usa el primero). `tz` es la zona horaria del navegador. */
export function exportHref(portfolioId: string | null | undefined, kinds: readonly ExportKind[], timeZone: string): string {
  const params = new URLSearchParams({ include: kinds.join(","), tz: timeZone });
  if (isPortfolioId(portfolioId)) params.set("portfolioId", portfolioId);
  return `/api/portfolio/export?${params.toString()}`;
}
