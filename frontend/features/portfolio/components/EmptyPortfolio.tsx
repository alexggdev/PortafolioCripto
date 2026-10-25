import { PlusIcon } from "@/shared/components/Icons";

/** Ilustración propia: una base con un gráfico de anillo encima. */
function Illustration() {
  return (
    <svg aria-hidden="true" viewBox="0 0 320 230" className="h-56 w-auto">
      <ellipse cx="160" cy="196" rx="112" ry="22" className="fill-rule" opacity="0.5" />
      <path d="M36 128v16l124 62 124-62v-16" className="fill-rule" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" opacity="0.9" />
      <path d="M160 66L284 128 160 190 36 128z" className="fill-surface" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" />
      <g transform="translate(160 126) scale(1 0.48)" fill="none" strokeWidth="20">
        <circle r="54" className="stroke-accent" strokeDasharray="130 340" transform="rotate(-100)" />
        <circle r="54" className="stroke-soft" strokeDasharray="80 340" strokeDashoffset="-142" transform="rotate(-100)" opacity="0.55" />
        <circle r="54" className="stroke-rule" strokeDasharray="90 340" strokeDashoffset="-236" transform="rotate(-100)" />
      </g>
      <ellipse cx="160" cy="62" rx="52" ry="14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeDasharray="5 6" opacity="0.6" />
      <rect x="140" y="26" width="40" height="22" rx="5" className="fill-accent" />
      <path d="M149 40l6-6 5 4 8-8" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/** Portafolio sin ninguna transacción: invita a añadir la primera moneda. */
export function EmptyPortfolio({ onAdd }: { onAdd: () => void }) {
  return (
    <section aria-label="Portafolio vacío" className="flex flex-col items-center px-4 py-10 text-center">
      <span className="text-soft">
        <Illustration />
      </span>
      <h2 className="condensed mt-4 text-3xl font-bold tracking-tight sm:text-4xl">Este portafolio necesita unos últimos retoques…</h2>
      <p className="mt-3 text-lg text-soft">Añade una moneda para empezar</p>
      <button
        type="button"
        onClick={onAdd}
        className="mt-6 inline-flex cursor-pointer items-center gap-2 rounded-lg bg-accent px-6 py-3 font-semibold text-on-accent transition-opacity hover:opacity-90"
      >
        <PlusIcon className="size-4" /> Añadir transacción
      </button>
    </section>
  );
}
