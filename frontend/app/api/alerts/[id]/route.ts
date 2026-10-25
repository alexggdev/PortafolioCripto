import { NextRequest } from "next/server";
import { forward, jsonError } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ id: string }> };

export async function DELETE(request: NextRequest, context: Context) {
  const { id } = await context.params;
  if (!/^\d{1,18}$/.test(id)) return jsonError(404, ["La alerta no existe."]);
  return forward(request, "DELETE", `/api/alerts/${id}`);
}
