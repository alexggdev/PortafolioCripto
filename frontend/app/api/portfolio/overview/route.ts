import { NextRequest } from "next/server";
import { forward } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  return forward(request, "GET", "/api/portfolio/overview");
}
