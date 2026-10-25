import { NextResponse } from "next/server";
import { ApiError } from "./api";

/** Respuesta de error de las rutas que hacen de intermediario con la API de .NET. */
export function proxyError(error: unknown) {
  const status = error instanceof ApiError && (error.status === 404 || error.status === 429) ? error.status : 502;
  const message =
    status === 404
      ? "Moneda no encontrada."
      : status === 429
        ? "Demasiadas peticiones. Espera un momento."
        : "No se pudieron cargar los datos.";
  return NextResponse.json({ error: message }, { status });
}

export const NO_STORE = { "Cache-Control": "no-store" } as const;
