"use client";

import { useState, type ReactNode } from "react";

/**
 * Globo que aparece al pasar el ratón (o al enfocar) sobre el contenido. Con `wide`, es un cuadro de varias líneas.
 * Se coloca con posición fija: no lo recortan las tablas con scroll.
 */
export function Hint({
  text,
  children,
  className = "",
  wide = false,
  below = false,
}: {
  text: ReactNode;
  children: ReactNode;
  className?: string;
  wide?: boolean;
  /** Abre el globo debajo del contenido en vez de encima. */
  below?: boolean;
}) {
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null);

  function show(element: HTMLElement) {
    const rect = element.getBoundingClientRect();
    const half = wide ? 192 : 0; // medio ancho del cuadro: no se sale de la pantalla
    const x = Math.min(Math.max(rect.left + rect.width / 2, half + 8), window.innerWidth - half - 8);
    setPos({ x, y: below ? rect.bottom + 8 : rect.top - 8 });
  }

  return (
    <span
      className={`inline-flex ${className}`}
      onPointerEnter={(event) => show(event.currentTarget)}
      onPointerLeave={() => setPos(null)}
      onFocus={(event) => show(event.currentTarget)}
      onBlur={() => setPos(null)}
    >
      {children}
      {pos && (
        <span
          role="tooltip"
          className={`pointer-events-none fixed z-50 -translate-x-1/2 ${below ? "" : "-translate-y-full"} rounded-lg bg-paper text-xs text-ink shadow-xl ring-1 ring-rule ${
            wide ? "w-96 whitespace-normal px-4 py-3 text-left font-normal leading-relaxed" : "whitespace-nowrap px-3 py-1.5 font-medium"
          }`}
          style={{ left: pos.x, top: pos.y }}
        >
          {text}
        </span>
      )}
    </span>
  );
}

/** La «i» de «¿Cómo se calcula?»: al pasar el ratón explica la fórmula en un cuadro con título. */
export function InfoHint({ children, below = false }: { children: ReactNode; below?: boolean }) {
  return (
    <Hint
      wide
      below={below}
      text={
        <>
          <span className="mb-1.5 block text-sm font-bold">¿Cómo se calcula?</span>
          {children}
        </>
      }
    >
      <span aria-label="Cómo se calcula" tabIndex={0} className="inline-flex size-4 cursor-help items-center justify-center rounded-full border border-soft text-[10px] font-semibold not-italic text-soft">
        i
      </span>
    </Hint>
  );
}
