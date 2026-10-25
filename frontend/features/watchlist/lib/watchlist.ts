import { extractErrors } from "../../../shared/lib/apiErrors";

export interface WatchResult {
  ok: boolean;
  /** 401 = sesión caducada (el llamador decide llevar al login). */
  status: number;
  errors: string[];
}

type FetchLike = (input: string, init: RequestInit) => Promise<Pick<Response, "ok" | "status" | "json">>;

/**
 * Sigue (PUT) o deja de seguir (DELETE) una moneda desde el navegador.
 * Nunca lanza: un fallo de red se devuelve como resultado con mensaje.
 */
export async function setWatched(
  slug: string,
  watched: boolean,
  doFetch: FetchLike = (input, init) => fetch(input, init),
): Promise<WatchResult> {
  try {
    const response = await doFetch(`/api/watchlist/${encodeURIComponent(slug)}`, {
      method: watched ? "PUT" : "DELETE",
    });
    if (response.ok) return { ok: true, status: response.status, errors: [] };

    const body: unknown = await response.json().catch(() => null);
    return {
      ok: false,
      status: response.status,
      errors: extractErrors(body, "No se pudo actualizar tu lista de seguimiento."),
    };
  } catch {
    return { ok: false, status: 0, errors: ["No se pudo conectar con el servidor. Inténtalo de nuevo."] };
  }
}
