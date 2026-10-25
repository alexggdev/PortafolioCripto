import type { ReactNode } from "react";

/** Capa «Recalculando» que se pone sobre un gráfico difuminado mientras llegan los datos nuevos. */
export function RecalculatingOverlay() {
  return (
    <div role="status" className="fade-in absolute inset-0 z-10 flex flex-col items-center justify-center text-center">
      <span aria-hidden="true" className="size-8 animate-spin rounded-full border-[3px] border-accent/25 border-t-accent" />
      <p className="mt-3 text-lg font-semibold">Recalculando</p>
      <p className="mt-1 text-sm text-soft">Esto puede tardar unos segundos</p>
    </div>
  );
}

/** Tarjeta flotante que sigue al cursor. Pasa al otro lado del cursor en la mitad derecha para no salirse. */
export function ChartTooltip({ x, width, children }: { x: number; width: number; children: ReactNode }) {
  const left = x > width * 0.55;
  return (
    <div
      className="pointer-events-none absolute top-6 z-20 whitespace-nowrap rounded-xl bg-paper px-4 py-3 text-sm shadow-xl ring-1 ring-rule"
      style={{ left: x, transform: left ? "translateX(calc(-100% - 14px))" : "translateX(14px)" }}
    >
      {children}
    </div>
  );
}

/** Punto de color de las filas del tooltip y de la leyenda. */
export function Dot({ className }: { className: string }) {
  return <span aria-hidden="true" className={`inline-block size-3 shrink-0 rounded-full ${className}`} />;
}

/** Texto de un valor que se oculta con «Ocultar saldos». */
export const HIDDEN_VALUE = "••••••";
