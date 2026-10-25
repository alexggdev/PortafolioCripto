"use client";

export default function ErrorPage({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="py-20">
      <h1 className="condensed text-3xl font-bold tracking-tight">No se pudieron cargar los datos</h1>
      <p className="mt-3 max-w-prose text-soft">
        El servidor de precios no responde en este momento. No es un problema de tu conexión. Vuelve a intentarlo en unos
        segundos.
      </p>
      <button
        type="button"
        onClick={reset}
        className="mt-6 border border-ink px-4 py-2 font-medium hover:border-accent hover:text-accent"
      >
        Reintentar
      </button>
    </div>
  );
}
