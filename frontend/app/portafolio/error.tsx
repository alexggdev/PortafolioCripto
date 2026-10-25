"use client";

import { useEffect } from "react";

/** Si la API no responde, la página no se rompe: se queda este aviso y «Reintentar» vuelve a cargar los datos. */
export default function PortfolioError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    const timer = setTimeout(reset, 5000);
    return () => clearTimeout(timer);
  }, [error, reset]);

  return (
    <main className="mx-auto max-w-xl px-4 py-24 text-center">
      <h1 className="text-xl font-bold">El servidor está tardando en responder</h1>
      <p className="mt-2 text-soft">Tus datos están a salvo. Se vuelve a intentar solo en unos segundos.</p>
      <button type="button" onClick={reset} className="mt-6 cursor-pointer rounded-lg bg-accent px-4 py-2.5 text-sm font-semibold text-on-accent hover:opacity-90">
        Reintentar ahora
      </button>
    </main>
  );
}
