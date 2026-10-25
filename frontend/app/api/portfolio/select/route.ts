import { NextRequest, NextResponse } from "next/server";
import { isPortfolioId } from "@/features/portfolio/lib/portfolioId";
import { parseScreen, screenCookie, SELECTED_COOKIE, VIEW_COOKIE } from "@/features/portfolio/lib/selectedPortfolio";
import { secureCookies } from "@/features/auth/lib/sessionCookies";

const OPTIONS = { httpOnly: true, sameSite: "lax", path: "/", maxAge: 60 * 60 * 24 * 90 } as const;

/**
 * Recuerda qué portafolio y qué pantalla se ven. Solo guarda preferencias; los datos siguen protegidos por la sesión.
 * Cuerpo: { id?, screen? } con screen = "portafolio" | "resumen" | "moneda:<slug>". Sin screen, vuelve al portafolio.
 */
export async function POST(request: NextRequest) {
  const body = (await request.json().catch(() => null)) as { id?: unknown; screen?: unknown } | null;
  if (body?.id !== undefined && !isPortfolioId(body.id)) return NextResponse.json({ error: "Portafolio no válido." }, { status: 400 });

  const response = new NextResponse(null, { status: 204 });
  const secure = secureCookies();
  if (isPortfolioId(body?.id)) response.cookies.set(SELECTED_COOKIE, body.id, { ...OPTIONS, secure });

  const value = screenCookie(parseScreen(typeof body?.screen === "string" ? body.screen : undefined));
  if (value) response.cookies.set(VIEW_COOKIE, value, { ...OPTIONS, secure });
  else response.cookies.delete(VIEW_COOKIE);
  return response;
}
