import { NextRequest } from "next/server";
import { isPortfolioId } from "@/features/portfolio/lib/portfolioId";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  if (!isPortfolioId(id)) return jsonError(404, ["El portafolio no existe."]);
  const body = await readBody(request);
  if (body === null) return jsonError(400, ["Los datos del portafolio no son válidos."]);
  return forward(request, "POST", `/api/portfolios/${id}/duplicate`, body);
}
