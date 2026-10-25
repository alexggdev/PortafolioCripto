import { headers } from "next/headers";

// Dirección de la API de .NET. Solo existe en el servidor de Next: el navegador nunca la conoce.
export const API_URL = (process.env.API_URL ?? "http://localhost:5080").replace(/\/+$/, "");

const TIMEOUT_MS = 8000;
const RETRY_DELAY_MS = 300;

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function wait(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * GET a la API de .NET desde el servidor de Next. Un reintento ante fallo de red o error 5xx.
 * Reenvía X-Forwarded-For para que el limitador de peticiones de la API vea la IP del visitante
 * y no la del servidor de Next (la API solo hace caso a esa cabecera si viene de localhost).
 */
export async function apiGet<T>(
  path: string,
  options: { timeoutMs?: number; retry?: boolean } = {},
): Promise<T> {
  const timeoutMs = options.timeoutMs ?? TIMEOUT_MS;
  const attempts = options.retry === false ? 1 : 2;
  const incoming = await headers();
  const forwarded = incoming.get("x-forwarded-for");

  const requestHeaders: Record<string, string> = { Accept: "application/json" };
  if (forwarded) requestHeaders["X-Forwarded-For"] = forwarded;

  let lastError: unknown;

  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      const response = await fetch(`${API_URL}${path}`, {
        headers: requestHeaders,
        cache: "no-store",
        signal: AbortSignal.timeout(timeoutMs),
      });

      if (response.ok) {
        return (await response.json()) as T;
      }

      const error = new ApiError(response.status, `La API respondió ${response.status}`);
      if (response.status < 500) throw error; // 4xx: reintentar no cambia nada
      lastError = error;
    } catch (error) {
      if (error instanceof ApiError && error.status < 500) throw error;
      lastError = error;
    }

    if (attempt < attempts - 1) await wait(RETRY_DELAY_MS);
  }

  throw lastError instanceof Error ? lastError : new Error("No se pudo conectar con la API");
}
