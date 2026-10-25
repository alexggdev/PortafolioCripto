import { NextRequest, NextResponse } from "next/server";
import { apiRequest } from "../../../shared/lib/apiRequest";
import { getSession } from "../../auth/lib/session";

// Intermediario entre el navegador y la API de .NET para las rutas privadas.
// El navegador nunca ve el JWT (va en una cookie httpOnly) ni la dirección de la API.

const NO_STORE = { "Cache-Control": "no-store" } as const;
const MAX_BODY_CHARS = 4000;

/** Las peticiones que cambian datos solo se aceptan si vienen de esta misma web (defensa extra contra CSRF). */
export function isSameOrigin(request: NextRequest): boolean {
  const origin = request.headers.get("origin");
  if (!origin) return false;
  try {
    const host = new URL(origin).host;
    return host === request.headers.get("host") || host === request.headers.get("x-forwarded-host");
  } catch {
    return false;
  }
}

export function jsonError(status: number, errors: string[]) {
  return NextResponse.json({ error: errors.join(" "), errors }, { status, headers: NO_STORE });
}

export async function readBody(request: NextRequest): Promise<string | null> {
  const text = await request.text();
  if (!text || text.length > MAX_BODY_CHARS) return null;
  try {
    JSON.parse(text);
  } catch {
    return null;
  }
  return text;
}

export async function forward(
  request: NextRequest,
  method: "GET" | "POST" | "PUT" | "DELETE",
  path: string,
  body?: string,
): Promise<NextResponse> {
  if (method !== "GET" && !isSameOrigin(request)) {
    return jsonError(403, ["Petición no permitida."]);
  }

  const session = await getSession();
  if (!session) return jsonError(401, ["Tu sesión ha caducado. Vuelve a entrar."]);

  const result = await apiRequest<unknown>(method, path, { token: session.token, body });

  if (result.ok) {
    if (result.status === 204) return new NextResponse(null, { status: 204, headers: NO_STORE });
    return NextResponse.json(result.data, { status: result.status, headers: NO_STORE });
  }

  const status = [400, 401, 404, 429].includes(result.status) ? result.status : 502;
  return jsonError(status, result.errors);
}
