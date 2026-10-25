import { NextRequest } from "next/server";
import { parseMoveBody } from "@/features/portfolio/lib/moveBody";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";
import { isValidSlug } from "@/features/market/lib/slug";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ slug: string }> };

// Mueve una moneda, con todo su historial, a otro portafolio del usuario.
export async function POST(request: NextRequest, context: Context) {
  const { slug } = await context.params;
  if (!isValidSlug(slug)) return jsonError(404, ["Moneda no encontrada."]);

  const raw = await readBody(request);
  const body = raw === null ? null : parseMoveBody(raw);
  if (body === null) return jsonError(400, ["Elige un portafolio de destino distinto del actual."]);

  return forward(request, "POST", `/api/portfolio/assets/${encodeURIComponent(slug)}/move`, JSON.stringify(body));
}
