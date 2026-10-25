import { NextRequest } from "next/server";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import { forward, jsonError, readBody } from "@/features/portfolio/lib/portfolioProxy";
import { isValidSlug } from "@/features/market/lib/slug";
import { TYPE_OPTIONS } from "@/features/portfolio/lib/transactions";
import type { TransactionType } from "@/shared/lib/types";

const TRANSACTION_TYPES: readonly string[] = TYPE_OPTIONS.map((o) => o.value);

export const dynamic = "force-dynamic";

function intParam(value: string | null, fallback: number, max: number): number {
  const n = Number.parseInt(value ?? "", 10);
  return Number.isFinite(n) ? Math.min(Math.max(n, 1), max) : fallback;
}

export async function GET(request: NextRequest) {
  const source = request.nextUrl.searchParams;
  const params = new URLSearchParams();

  const slug = source.get("assetSlug");
  if (slug) {
    if (!isValidSlug(slug)) return jsonError(400, ["Moneda no válida."]);
    params.set("assetSlug", slug);
  }
  const type = source.get("type");
  if (type) {
    if (!TRANSACTION_TYPES.includes(type as TransactionType)) return jsonError(400, ["Tipo de transacción no válido."]);
    params.set("type", type);
  }
  params.set("page", String(intParam(source.get("page"), 1, 100_000)));
  params.set("pageSize", String(intParam(source.get("pageSize"), 20, 100)));

  return forward(request, "GET", `/api/portfolio/transactions?${params}${portfolioQuery(source.get("portfolioId"))}`);
}

export async function POST(request: NextRequest) {
  const body = await readBody(request);
  if (body === null) return jsonError(400, ["Los datos de la transacción no son válidos."]);
  return forward(request, "POST", "/api/portfolio/transactions", body);
}
