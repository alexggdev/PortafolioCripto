import { NextRequest } from "next/server";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  return forward(request, "GET", "/api/alerts");
}

export async function POST(request: NextRequest) {
  const body = await readBody(request);
  if (body === null) return jsonError(400, ["Los datos de la alerta no son válidos."]);
  return forward(request, "POST", "/api/alerts", body);
}
