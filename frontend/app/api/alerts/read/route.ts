import { NextRequest } from "next/server";
import { forward } from "@/features/portfolio/lib/portfolioProxy";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  return forward(request, "POST", "/api/alerts/read");
}
