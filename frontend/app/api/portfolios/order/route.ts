import { NextRequest } from "next/server";
import { isPortfolioId } from "@/features/portfolio/lib/portfolioId";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function PUT(request: NextRequest) {
  const body = await readBody(request);
  const ids = body === null ? null : (JSON.parse(body) as { ids?: unknown }).ids;
  if (!Array.isArray(ids) || !ids.every(isPortfolioId)) return jsonError(400, ["El orden no es válido."]);
  return forward(request, "PUT", "/api/portfolios/order", JSON.stringify({ ids }));
}
