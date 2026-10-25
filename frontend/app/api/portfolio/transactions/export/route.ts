import { NextRequest, NextResponse } from "next/server";
import { API_URL } from "@/shared/lib/api";
import { jsonError } from "@/features/portfolio/lib/portfolioProxy";
import { isPortfolioId } from "@/features/portfolio/lib/portfolioId";
import { getSession } from "@/features/auth/lib/session";

export const dynamic = "force-dynamic";

// Descarga del CSV. No pasa por forward() porque la respuesta no es JSON.
// Es una lectura (GET), así que no necesita la comprobación de origen de las escrituras.
export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session) {
    // Un enlace de descarga no puede mostrar un aviso: se lleva al login y se vuelve al portafolio.
    return NextResponse.redirect(new URL("/api/session/expired?next=%2Fportafolio", request.url));
  }

  const format = request.nextUrl.searchParams.get("format") === "excel" ? "excel" : "standard";
  const portfolioId = request.nextUrl.searchParams.get("portfolioId");
  if (portfolioId !== null && !isPortfolioId(portfolioId)) return jsonError(404, ["El portafolio no existe."]);

  const query = new URLSearchParams({ format });
  if (portfolioId) query.set("portfolioId", portfolioId);

  let upstream: Response;
  try {
    upstream = await fetch(`${API_URL}/api/portfolio/transactions/export?${query.toString()}`, {
      headers: { Authorization: `Bearer ${session.token}`, Accept: "text/csv" },
      cache: "no-store",
      signal: AbortSignal.timeout(20_000),
    });
  } catch {
    return jsonError(502, ["No se pudo conectar con el servidor."]);
  }

  if (upstream.status === 401) {
    return NextResponse.redirect(new URL("/api/session/expired?next=%2Fportafolio", request.url));
  }
  if (!upstream.ok) {
    return jsonError([404, 429].includes(upstream.status) ? upstream.status : 502, ["No se pudo generar el archivo."]);
  }

  return new NextResponse(upstream.body, {
    status: 200,
    headers: {
      "Content-Type": "text/csv; charset=utf-8",
      "Content-Disposition": upstream.headers.get("content-disposition") ?? 'attachment; filename="transacciones.csv"',
      "Cache-Control": "no-store",
    },
  });
}
