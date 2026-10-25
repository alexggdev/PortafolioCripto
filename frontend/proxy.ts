import { NextRequest, NextResponse } from "next/server";
import { clientIpFrom } from "@/shared/lib/clientIp";
import { isTokenExpired, jwtExpiresAtMs } from "@/features/auth/lib/jwt";
import {
  cookiesFor,
  EMAIL_COOKIE,
  mergeCookieHeader,
  needsRefresh,
  REFRESH_COOKIE,
  removeCookies,
  secureCookies,
  TOKEN_COOKIE,
  type AuthPayload,
} from "@/features/auth/lib/sessionCookies";

// (Next 16: este archivo se llamaba middleware.ts; ahora se llama proxy.ts.)
// Renueva el JWT con el refresh token antes de que la página (o el proxy hacia la API) lo necesite.
// Tiene que ser aquí: las páginas del servidor no pueden escribir cookies, el middleware sí.

const API_URL = (process.env.API_URL ?? "http://localhost:5080").replace(/\/+$/, "");
const RENEW_MARGIN_MS = 60_000;

type Outcome = { kind: "ok"; auth: AuthPayload } | { kind: "rejected" } | { kind: "unavailable" };

async function callRefresh(refreshToken: string, forwardedFor: string | null): Promise<Outcome & { status?: number }> {
  try {
    const response = await fetch(`${API_URL}/api/auth/refresh`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
        ...(forwardedFor ? { "X-Forwarded-For": forwardedFor } : {}),
      },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    });
    if (response.ok) return { kind: "ok", auth: (await response.json()) as AuthPayload };
    if (response.status === 401) return { kind: "rejected" };
    return { kind: "unavailable", status: response.status };
  } catch {
    return { kind: "unavailable" };
  }
}

// Peticiones simultáneas (página, alertas, refrescos automáticos) llegan con el mismo refresh token viejo.
// Como cada token vale una sola vez, solo la primera llama a la API: las demás reutilizan su resultado durante unos segundos.
const SHARE_MS = 30_000;
const shared = new Map<string, { at: number; result: Promise<Outcome & { status?: number }> }>();

function refreshShared(refreshToken: string, forwardedFor: string | null) {
  const now = Date.now();
  for (const [key, entry] of shared) if (now - entry.at > SHARE_MS) shared.delete(key);

  const existing = shared.get(refreshToken);
  if (existing) return existing.result;

  const result = callRefresh(refreshToken, forwardedFor);
  shared.set(refreshToken, { at: now, result });
  // Solo se recuerdan los éxitos: un fallo debe poder reintentarse.
  void result.then((outcome) => {
    if (outcome.kind !== "ok") shared.delete(refreshToken);
  });
  return result;
}

export async function proxy(request: NextRequest) {
  // Las acciones de servidor (login, logout) gestionan sus propias cookies.
  if (request.headers.has("next-action")) return NextResponse.next();

  const jwt = request.cookies.get(TOKEN_COOKIE)?.value;
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  const expired = (token: string) => isTokenExpired(token, Date.now(), RENEW_MARGIN_MS);
  if (!needsRefresh(jwt, refreshToken, expired)) return NextResponse.next();

  const forwardedFor = clientIpFrom(request.headers.get("x-forwarded-for"));
  let outcome = await refreshShared(refreshToken!, forwardedFor);

  // 409: otra petición está renovando ese mismo token. Se espera un instante y se reintenta una vez.
  if (outcome.kind === "unavailable" && outcome.status === 409) {
    await new Promise((resolve) => setTimeout(resolve, 400));
    outcome = await refreshShared(refreshToken!, forwardedFor);
  }

  if (outcome.kind === "ok") {
    const secure = secureCookies();
    const cookies = cookiesFor(outcome.auth, jwtExpiresAtMs(outcome.auth.token), Date.now(), secure);

    // La petición en curso ve ya las cookies nuevas, y el navegador las recibe en la respuesta.
    const updates = Object.fromEntries(cookies.map((c) => [c.name, c.value]));
    const headers = new Headers(request.headers);
    headers.set("cookie", mergeCookieHeader(request.headers.get("cookie"), updates));

    const response = NextResponse.next({ request: { headers } });
    for (const c of cookies) response.cookies.set(c.name, c.value, c.options);
    return response;
  }

  if (outcome.kind === "rejected") {
    // Refresh token caducado, cerrado o robado: se limpia todo y la página mandará al login.
    const headers = new Headers(request.headers);
    headers.set("cookie", removeCookies(request.headers.get("cookie"), [TOKEN_COOKIE, REFRESH_COOKIE, EMAIL_COOKIE]));
    const response = NextResponse.next({ request: { headers } });
    response.cookies.delete(TOKEN_COOKIE);
    response.cookies.delete(REFRESH_COOKIE);
    response.cookies.delete(EMAIL_COOKIE);
    return response;
  }

  // La API no responde: no se toca nada. Con la API caída la página fallará de todos modos, y al volver se renovará.
  return NextResponse.next();
}

export const config = {
  // No se ejecuta en archivos estáticos ni en la ruta que borra la sesión.
  matcher: ["/((?!_next/static|_next/image|favicon.ico|icon.svg|robots.txt|api/session/expired).*)"],
};
