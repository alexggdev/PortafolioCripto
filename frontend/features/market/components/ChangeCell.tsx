import { directionOf, formatPct } from "@/shared/lib/format";

// La barra llena equivale a una variación de ±10 %; lo que pase de ahí se recorta.
const FULL_BAR_PCT = 10;

const ARROW = { up: "▲", down: "▼", flat: "" } as const;
const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;

/**
 * Variación porcentual con dos señales redundantes: el número con signo y flecha (lo que leen los
 * lectores de pantalla) y una barra desde el centro hacia la derecha (sube) o la izquierda (baja),
 * para comparar de un vistazo la fuerza del movimiento entre monedas.
 */
export function ChangeCell({ value }: { value: number | null }) {
  const direction = directionOf(value);
  const half = value === null ? 0 : Math.min(Math.abs(value) / FULL_BAR_PCT, 1) * 50;

  return (
    <div className={`flex items-center justify-end gap-3 ${COLOR[direction]}`}>
      <span aria-hidden="true" className="relative hidden h-2.5 w-20 sm:block">
        <span className="absolute inset-y-0 left-1/2 w-px bg-rule" />
        {direction !== "flat" && (
          <span
            className="absolute inset-y-0 bg-current"
            style={direction === "up" ? { left: "50%", width: `${half}%` } : { right: "50%", width: `${half}%` }}
          />
        )}
      </span>
      <span className="text-right tabular-nums sm:min-w-[5.5rem]">
        <span aria-hidden="true" className="mr-1 text-[0.65em] align-middle">
          {ARROW[direction]}
        </span>
        {formatPct(value)}
      </span>
    </div>
  );
}
