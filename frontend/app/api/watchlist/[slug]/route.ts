import { NextRequest } from "next/server";
import { forward, jsonError } from "@/features/portfolio/lib/portfolioProxy";
import { isValidSlug } from "@/features/market/lib/slug";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ slug: string }> };

export async function PUT(request: NextRequest, context: Context) {
  const { slug } = await context.params;
  if (!isValidSlug(slug)) return jsonError(404, ["La moneda no existe."]);
  return forward(request, "PUT", `/api/watchlist/${encodeURIComponent(slug)}`);
}

export async function DELETE(request: NextRequest, context: Context) {
  const { slug } = await context.params;
  if (!isValidSlug(slug)) return jsonError(404, ["La moneda no existe."]);
  return forward(request, "DELETE", `/api/watchlist/${encodeURIComponent(slug)}`);
}
