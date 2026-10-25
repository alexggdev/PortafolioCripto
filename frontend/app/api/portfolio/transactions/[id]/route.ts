import { NextRequest } from "next/server";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ id: string }> };

async function parseId(context: Context): Promise<number | null> {
  const { id } = await context.params;
  return /^\d{1,18}$/.test(id) ? Number(id) : null;
}

export async function PUT(request: NextRequest, context: Context) {
  const id = await parseId(context);
  if (id === null) return jsonError(404, ["Transacción no encontrada."]);
  const body = await readBody(request);
  if (body === null) return jsonError(400, ["Los datos de la transacción no son válidos."]);
  return forward(request, "PUT", `/api/portfolio/transactions/${id}`, body);
}

export async function DELETE(request: NextRequest, context: Context) {
  const id = await parseId(context);
  if (id === null) return jsonError(404, ["Transacción no encontrada."]);
  return forward(request, "DELETE", `/api/portfolio/transactions/${id}`);
}
