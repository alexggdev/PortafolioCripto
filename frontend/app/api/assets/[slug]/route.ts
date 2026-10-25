import { NextRequest, NextResponse } from "next/server";
import { apiGet } from "@/shared/lib/api";
import { NO_STORE, proxyError } from "@/shared/lib/proxy";
import { isValidSlug } from "@/features/market/lib/slug";
import type { AssetSummary } from "@/shared/lib/types";

export const dynamic = "force-dynamic";

export async function GET(_request: NextRequest, { params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  if (!isValidSlug(slug)) {
    return NextResponse.json({ error: "Moneda no encontrada." }, { status: 404 });
  }

  try {
    const data = await apiGet<AssetSummary>(`/api/assets/${encodeURIComponent(slug)}`);
    return NextResponse.json(data, { headers: NO_STORE });
  } catch (error) {
    return proxyError(error);
  }
}
