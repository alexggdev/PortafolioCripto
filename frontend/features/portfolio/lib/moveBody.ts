import { isPortfolioId } from "./portfolioId";

export interface MoveBody {
  fromPortfolioId: string;
  toPortfolioId: string;
}

/** Valida el cuerpo de «mover moneda»: dos ids de portafolio distintos. Devuelve null si algo no cuadra. */
export function parseMoveBody(text: string): MoveBody | null {
  try {
    const value = JSON.parse(text) as Record<string, unknown> | null;
    if (!value || typeof value !== "object") return null;
    const { fromPortfolioId, toPortfolioId } = value;
    if (!isPortfolioId(fromPortfolioId) || !isPortfolioId(toPortfolioId)) return null;
    if (fromPortfolioId.toLowerCase() === toPortfolioId.toLowerCase()) return null;
    return { fromPortfolioId, toPortfolioId };
  } catch {
    return null;
  }
}
