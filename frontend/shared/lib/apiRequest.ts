import { headers } from "next/headers";
import { API_URL } from "./api";
import { extractErrors } from "./apiErrors";

export interface ApiResult<T> {
  ok: boolean;
  status: number;
  data: T | null;
  errors: string[];
}

const TIMEOUT_MS = 8000;

function fallbackFor(status: number): string {
  if (status === 401) return "Tu sesión ha caducado. Vuelve a entrar.";
  if (status === 429) return "Demasiadas peticiones. Espera un momento.";
  if (status === 404) return "No se encontró lo que pedías.";
  return "No se pudo completar la operación. Inténtalo de nuevo.";
}

/**
 * Petición a la API de .NET desde el servidor de Next, con el JWT si hay sesión.
 * Nunca lanza: devuelve el estado y los mensajes de error en español.
 * Solo las lecturas (GET) se reintentan: repetir un POST podría guardar una transacción dos veces.
 */
export async function apiRequest<T>(
  method: "GET" | "POST" | "PUT" | "DELETE",
  path: string,
  options: { token?: string; body?: string } = {},
): Promise<ApiResult<T>> {
  const incoming = await headers();
  const forwarded = incoming.get("x-forwarded-for");

  const requestHeaders: Record<string, string> = { Accept: "application/json" };
  if (forwarded) requestHeaders["X-Forwarded-For"] = forwarded;
  if (options.token) requestHeaders.Authorization = `Bearer ${options.token}`;
  if (options.body !== undefined) requestHeaders["Content-Type"] = "application/json";

  const attempts = method === "GET" ? 2 : 1;
  let last: ApiResult<T> = { ok: false, status: 502, data: null, errors: ["No se pudo conectar con el servidor."] };

  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      const response = await fetch(`${API_URL}${path}`, {
        method,
        headers: requestHeaders,
        body: options.body,
        cache: "no-store",
        signal: AbortSignal.timeout(TIMEOUT_MS),
      });

      const text = response.status === 204 ? "" : await response.text();
      let json: unknown = null;
      if (text) {
        try {
          json = JSON.parse(text);
        } catch {
          json = null;
        }
      }

      if (response.ok) return { ok: true, status: response.status, data: json as T | null, errors: [] };

      last = { ok: false, status: response.status, data: null, errors: extractErrors(json, fallbackFor(response.status)) };
      if (response.status < 500) return last;
    } catch {
      last = { ok: false, status: 502, data: null, errors: ["No se pudo conectar con el servidor."] };
    }
    if (attempt < attempts - 1) await new Promise((resolve) => setTimeout(resolve, 300));
  }
  return last;
}
