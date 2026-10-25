import { NextRequest } from "next/server";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import { forward } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const query = portfolioQuery(request.nextUrl.searchParams.get("portfolioId")).replace("&", "?");
  return forward(request, "GET", `/api/portfolio${query}`);
}
