import { isPortfolioId } from "./portfolioId";
import { isValidSlug } from "../../market/lib/slug";

// Qué pantalla del portafolio se ve y cuál está abierto se recuerda en cookies, no en la dirección:
// toda la sección vive en /portafolio.

export const SELECTED_COOKIE = "cp_portfolio";
export const VIEW_COOKIE = "cp_view";

export type PortfolioScreen = { kind: "portafolio" } | { kind: "resumen" } | { kind: "moneda"; slug: string };

/** Id guardado en la cookie, o null si no hay o no es un GUID válido. */
export function selectedId(raw: string | undefined): string | null {
  return isPortfolioId(raw) ? raw : null;
}

/** Pantalla guardada: «resumen», «moneda:<slug>» o, si no hay o no vale, el portafolio. */
export function parseScreen(raw: string | undefined): PortfolioScreen {
  if (raw === "resumen") return { kind: "resumen" };
  if (raw?.startsWith("moneda:")) {
    const slug = raw.slice("moneda:".length);
    if (isValidSlug(slug)) return { kind: "moneda", slug };
  }
  return { kind: "portafolio" };
}

/** Valor de la cookie para una pantalla; null para el portafolio (sin cookie). */
export function screenCookie(screen: PortfolioScreen): string | null {
  if (screen.kind === "resumen") return "resumen";
  if (screen.kind === "moneda") return `moneda:${screen.slug}`;
  return null;
}

/** El portafolio que se abre: el elegido (si sigue existiendo), si no el predeterminado y, si no hay, ninguno. */
export function pickPortfolio<T extends { id: string; isDefault: boolean }>(portfolios: readonly T[], chosen: string | null): T | null {
  return portfolios.find((x) => x.id === chosen) ?? portfolios.find((x) => x.isDefault) ?? null;
}
