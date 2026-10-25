import { NextRequest } from "next/server";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import { forward, jsonError } from "@/features/portfolio/lib/portfolioProxy";
import { isValidSlug } from "@/features/market/lib/slug";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ slug: string }> };

// Quita una moneda del portafolio indicado: borra todas sus transacciones.
export async function DELETE(request: NextRequest, context: Context) {
  const { slug } = await context.params;
  if (!isValidSlug(slug)) return jsonError(404, ["Moneda no encontrada."]);

  const query = portfolioQuery(request.nextUrl.searchParams.get("portfolioId")).replace(/^&/, "?");
  return forward(request, "DELETE", `/api/portfolio/assets/${encodeURIComponent(slug)}${query}`);
}
