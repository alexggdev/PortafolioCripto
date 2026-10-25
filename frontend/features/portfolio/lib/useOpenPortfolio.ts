"use client";

import { usePathname, useRouter } from "next/navigation";
import { useCallback } from "react";

async function save(id: string | undefined, screen: string): Promise<void> {
  await fetch("/api/portfolio/select", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ...(id ? { id } : {}), screen }),
  }).catch(() => undefined);
}

/** Solo guarda el portafolio abierto (sin navegar): para el que se acaba de crear o duplicar. */
export async function rememberPortfolio(id: string | undefined): Promise<void> {
  if (id) await save(id, "portafolio");
}

/**
 * Cambia de pantalla dentro de /portafolio: guarda cuál y recarga los datos. La dirección no cambia
 * (desde otra página del sitio, vuelve a /portafolio).
 */
export function usePortfolioNav() {
  const router = useRouter();
  const pathname = usePathname();

  const show = useCallback(
    async (screen: string, id?: string): Promise<void> => {
      await save(id, screen);
      if (pathname === "/portafolio") router.refresh();
      else router.push("/portafolio");
    },
    [pathname, router],
  );

  return {
    openPortfolio: useCallback((id: string) => show("portafolio", id), [show]),
    openOverview: useCallback(() => show("resumen"), [show]),
    openCoin: useCallback((slug: string) => show(`moneda:${slug}`), [show]),
    backToPortfolio: useCallback(() => show("portafolio"), [show]),
  };
}
