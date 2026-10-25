import { NextRequest } from "next/server";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  return forward(request, "GET", "/api/portfolios");
}

export async function POST(request: NextRequest) {
  const body = await readBody(request);
  if (body === null) return jsonError(400, ["Los datos del portafolio no son válidos."]);
  return forward(request, "POST", "/api/portfolios", body);
}
