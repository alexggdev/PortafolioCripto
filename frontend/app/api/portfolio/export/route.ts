import { NextRequest, NextResponse } from "next/server";
import { API_URL } from "@/shared/lib/api";
import { exportBaseName, exportFileName, parseKinds, portfolioOverviewCsv, safeTimeZone, transactionHistoryCsv } from "@/features/portfolio/lib/exportCsv";
import { isPortfolioId } from "@/features/portfolio/lib/portfolioId";
import { jsonError } from "@/features/portfolio/lib/portfolioProxy";
import { getSession } from "@/features/auth/lib/session";
import type { PagedResult, PortfolioSummary, TransactionDto } from "@/shared/lib/types";
import { createZip } from "@/features/portfolio/lib/zip";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 100;
const MAX_PAGES = 200;
const TIMEOUT_MS = 20_000;

class UpstreamError extends Error {
  constructor(readonly status: number) {
    super(`upstream ${status}`);
  }
}

async function getJson<T>(path: string, token: string): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
    cache: "no-store",
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
  if (!response.ok) throw new UpstreamError(response.status);
  return (await response.json()) as T;
}

async function allTransactions(token: string, portfolioQuery: string): Promise<TransactionDto[]> {
  const items: TransactionDto[] = [];
  for (let page = 1; page <= MAX_PAGES; page++) {
    const result = await getJson<PagedResult<TransactionDto>>(`/api/portfolio/transactions?page=${page}&pageSize=${PAGE_SIZE}${portfolioQuery}`, token);
    items.push(...result.items);
    if (page >= result.totalPages) break;
  }
  return items;
}

// Descarga de «Exportar»: un CSV si se pide un solo archivo, o un ZIP con los dos. La respuesta no es JSON,
// así que no pasa por forward(). Es una lectura (GET): no necesita la comprobación de origen de las escrituras.
export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session) return NextResponse.redirect(new URL("/api/session/expired?next=%2Fportafolio", request.url));

  const params = request.nextUrl.searchParams;
  const kinds = parseKinds(params.get("include"));
  if (kinds.length === 0) return jsonError(400, ["Elige qué quieres exportar."]);

  const portfolioId = params.get("portfolioId");
  if (portfolioId !== null && !isPortfolioId(portfolioId)) return jsonError(404, ["El portafolio no existe."]);
  const portfolioQuery = portfolioId ? `&portfolioId=${portfolioId}` : "";
  const timeZone = safeTimeZone(params.get("tz"));
  const now = new Date();

  try {
    const [summary, transactions] = await Promise.all([
      getJson<PortfolioSummary>(`/api/portfolio?${portfolioQuery.slice(1)}`, session.token),
      kinds.includes("history") ? allTransactions(session.token, portfolioQuery) : Promise.resolve([] as TransactionDto[]),
    ]);

    const base = exportBaseName(summary.name);
    const files = kinds.map((kind) => ({
      name: exportFileName(base, [kind]),
      data: Buffer.from(kind === "history" ? transactionHistoryCsv(transactions, timeZone, now) : portfolioOverviewCsv(summary, timeZone, now), "utf8"),
    }));

    const single = files.length === 1;
    const body = single ? files[0].data : createZip(files, now);
    const fileName = exportFileName(base, kinds);
    return new NextResponse(new Uint8Array(body), {
      status: 200,
      headers: {
        "Content-Type": single ? "text/csv; charset=utf-8" : "application/zip",
        "Content-Disposition": `attachment; filename="${fileName}"`,
        "Cache-Control": "no-store",
      },
    });
  } catch (error) {
    if (error instanceof UpstreamError) {
      if (error.status === 401) return NextResponse.redirect(new URL("/api/session/expired?next=%2Fportafolio", request.url));
      return jsonError([404, 429].includes(error.status) ? error.status : 502, ["No se pudo generar el archivo."]);
    }
    return jsonError(502, ["No se pudo conectar con el servidor."]);
  }
}
