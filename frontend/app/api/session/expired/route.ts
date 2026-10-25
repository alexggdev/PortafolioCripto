import { NextRequest, NextResponse } from "next/server";
import { EMAIL_COOKIE, REFRESH_COOKIE, TOKEN_COOKIE } from "@/features/auth/lib/session";
import { safeNextPath } from "@/features/auth/lib/safeNext";

export const dynamic = "force-dynamic";

// Sesión caducada o rechazada por la API: borra las cookies y lleva al login.
// Se hace aquí (y no en la propia página) para que el login no vuelva a mandar al usuario a una página que falla: bucle.
export async function GET(request: NextRequest) {
  const next = safeNextPath(request.nextUrl.searchParams.get("next"));
  const response = new NextResponse(null, {
    status: 307,
    headers: { Location: `/login?next=${encodeURIComponent(next)}&expired=1`, "Cache-Control": "no-store" },
  });
  response.cookies.delete(TOKEN_COOKIE);
  response.cookies.delete(EMAIL_COOKIE);
  response.cookies.delete(REFRESH_COOKIE);
  return response;
}
