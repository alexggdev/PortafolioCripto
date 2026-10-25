import { NextRequest } from "next/server";
import { isPortfolioId } from "@/features/portfolio/lib/portfolioId";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ id: string }> };

export async function PUT(request: NextRequest, context: Context) {
  const { id } = await context.params;
  if (!isPortfolioId(id)) return jsonError(404, ["El portafolio no existe."]);
  const body = await readBody(request);
  if (body === null) return jsonError(400, ["Los datos del portafolio no son válidos."]);
  return forward(request, "PUT", `/api/portfolios/${id}`, body);
}

export async function DELETE(request: NextRequest, context: Context) {
  const { id } = await context.params;
  if (!isPortfolioId(id)) return jsonError(404, ["El portafolio no existe."]);
  return forward(request, "DELETE", `/api/portfolios/${id}`);
}
