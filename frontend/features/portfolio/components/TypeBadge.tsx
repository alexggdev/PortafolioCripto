import { typeVisual } from "@/features/portfolio/lib/transactionRows";
import type { TransactionType } from "@/shared/lib/types";

const PATHS = {
  swap: ["M6 8h11M14 5l3 3-3 3", "M18 16H7M10 13l-3 3 3 3"],
  in: ["M12 4v10M8 10l4 4 4-4", "M5 19h14"],
  out: ["M12 16V6M8 10l4-4 4 4", "M5 19h14"],
} as const;

/** Círculo del tipo de transacción: verde si suma a lo que tienes, rojo si resta; con flechas de intercambio, entrada o salida. */
export function TypeBadge({ type, size = 32 }: { type: TransactionType; size?: number }) {
  const { glyph, tone } = typeVisual(type);
  return (
    <span
      aria-hidden="true"
      className={`inline-flex shrink-0 items-center justify-center rounded-full text-white ${tone === "up" ? "bg-up" : "bg-down"}`}
      style={{ width: size, height: size }}
    >
      <svg viewBox="0 0 24 24" className="size-[60%]" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        {PATHS[glyph].map((d) => (
          <path key={d} d={d} />
        ))}
      </svg>
    </span>
  );
}
