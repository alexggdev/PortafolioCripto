import { NextRequest, NextResponse } from "next/server";
import { apiGet } from "@/shared/lib/api";
import { NO_STORE, proxyError } from "@/shared/lib/proxy";
import { parseRange } from "@/features/market/lib/ranges";
import { isValidSlug } from "@/features/market/lib/slug";
import type { AssetChart } from "@/shared/lib/types";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest, { params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  if (!isValidSlug(slug)) {
    return NextResponse.json({ error: "Moneda no encontrada." }, { status: 404 });
  }

  const range = parseRange(request.nextUrl.searchParams.get("range"));

  try {
    const data = await apiGet<AssetChart>(`/api/assets/${encodeURIComponent(slug)}/chart?range=${range}`);
    return NextResponse.json(data, { headers: NO_STORE });
  } catch (error) {
    return proxyError(error);
  }
}
