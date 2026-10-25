"use client";

// Último recurso: falla el propio diseño general de la web. Sustituye a todo, así que lleva su <html> y estilos propios.
export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="es">
      <body style={{ fontFamily: "system-ui, sans-serif", margin: 0, padding: "4rem 1.5rem", maxWidth: "40rem" }}>
        <h1 style={{ fontSize: "1.75rem", margin: 0 }}>Algo ha ido mal</h1>
        <p style={{ marginTop: "0.75rem", color: "#55645b" }}>
          La web no ha podido cargarse. No es un problema de tu conexión. Vuelve a intentarlo en unos segundos.
        </p>
        <button
          type="button"
          onClick={reset}
          style={{ marginTop: "1.5rem", padding: "0.5rem 1rem", border: "1px solid currentColor", background: "none", cursor: "pointer" }}
        >
          Reintentar
        </button>
      </body>
    </html>
  );
}
