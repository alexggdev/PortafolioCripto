import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import type { PortfolioInfo } from "@/shared/lib/types";

// Llamadas del navegador a las rutas /api/portfolios (que reenvían a la API de .NET).

export interface ApiResult<T> {
  ok: boolean;
  data: T | null;
  errors: string[];
}

async function call<T>(method: "POST" | "PUT" | "DELETE", url: string, body?: unknown): Promise<ApiResult<T>> {
  try {
    const response = await fetch(url, {
      method,
      headers: body === undefined ? undefined : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (response.status === 401) {
      redirectToLogin();
      return { ok: false, data: null, errors: [] };
    }
    const json = (await response.json().catch(() => null)) as (T & { errors?: string[]; error?: string }) | null;
    if (!response.ok) {
      return { ok: false, data: null, errors: json?.errors?.length ? json.errors : [json?.error ?? "No se pudo completar la operación."] };
    }
    return { ok: true, data: json, errors: [] };
  } catch {
    return { ok: false, data: null, errors: ["No se pudo conectar con el servidor. Inténtalo de nuevo."] };
  }
}

export const createPortfolio = (name: string, avatar: number) => call<PortfolioInfo>("POST", "/api/portfolios", { name, avatar });

/** Solo se envía lo que cambia. */
export const updatePortfolio = (id: string, changes: { name?: string; avatar?: number; includeInTotal?: boolean; isDefault?: boolean }) =>
  call<PortfolioInfo>("PUT", `/api/portfolios/${id}`, changes);

export const duplicatePortfolio = (id: string, name: string) => call<PortfolioInfo>("POST", `/api/portfolios/${id}/duplicate`, { name });

export const deletePortfolio = (id: string) => call<null>("DELETE", `/api/portfolios/${id}`);

export const reorderPortfolios = (ids: readonly string[]) => call<null>("PUT", "/api/portfolios/order", { ids });
