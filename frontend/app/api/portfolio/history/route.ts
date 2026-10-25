import { NextRequest } from "next/server";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import { forward } from "@/features/portfolio/lib/portfolioProxy";
import { parseRange } from "@/features/market/lib/ranges";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const range = parseRange(request.nextUrl.searchParams.get("range"));
  const portfolio = portfolioQuery(request.nextUrl.searchParams.get("portfolioId"));
  return forward(request, "GET", `/api/portfolio/history?range=${range}${portfolio}`);
}
