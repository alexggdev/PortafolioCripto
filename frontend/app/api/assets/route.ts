import { NextRequest, NextResponse } from "next/server";
import { ApiError, apiGet } from "@/shared/lib/api";
import { parseListQuery, toApiQuery } from "@/features/market/lib/query";
import type { AssetSummary, PagedResult } from "@/shared/lib/types";

// El navegador actualiza los precios llamando aquí (y no a la API de .NET directamente):
// así la dirección de la API no se expone y no hace falta CORS.
export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const query = parseListQuery(Object.fromEntries(request.nextUrl.searchParams));

  try {
    const data = await apiGet<PagedResult<AssetSummary>>(`/api/assets?${toApiQuery(query)}`);
    return NextResponse.json(data, { headers: { "Cache-Control": "no-store" } });
  } catch (error) {
    const status = error instanceof ApiError && error.status === 429 ? 429 : 502;
    return NextResponse.json({ error: "No se pudieron cargar los precios." }, { status });
  }
}
