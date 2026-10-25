"use client";

import { useRouter } from "next/navigation";

/** Aviso cuando la API no responde: la página no se rompe y se puede reintentar sin recargar a mano. */
export function LoadError({ message }: { message: string }) {
  const router = useRouter();
  return (
    <div role="alert" className="mt-8 border-l-2 border-down bg-surface px-4 py-5">
      <p className="font-medium">No se pudo cargar esta página.</p>
      <p className="mt-1 text-soft">{message}</p>
      <button type="button" onClick={() => router.refresh()} className="mt-4 bg-accent px-4 py-2 font-medium text-on-accent">
        Reintentar
      </button>
    </div>
  );
}
