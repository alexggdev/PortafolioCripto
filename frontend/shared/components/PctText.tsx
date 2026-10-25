import { directionOf, formatPct } from "@/shared/lib/format";

const ARROW = { up: "▲", down: "▼", flat: "" } as const;
const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;

/** Variación porcentual con signo y flecha (nunca solo color). */
export function PctText({ value }: { value: number | null | undefined }) {
  const direction = directionOf(value);
  return (
    <span className={`tabular-nums ${COLOR[direction]}`}>
      <span aria-hidden="true" className="mr-1 text-[0.65em] align-middle">
        {ARROW[direction]}
      </span>
      {formatPct(value)}
    </span>
  );
}
