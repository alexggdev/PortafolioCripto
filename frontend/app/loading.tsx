import { SKELETON } from "@/shared/lib/ui";

// Entre una página y otra: unas barras que parpadean, sin texto.
export default function Loading() {
  return (
    <div role="status" aria-busy="true" aria-label="Cargando" className="space-y-4 py-8">
      <span className={`block h-9 w-64 ${SKELETON}`} />
      <span className={`block h-4 w-96 max-w-full ${SKELETON}`} />
      <div className="space-y-3 pt-4">
        {Array.from({ length: 8 }, (_, i) => (
          <span key={i} className={`block h-12 ${SKELETON}`} />
        ))}
      </div>
    </div>
  );
}
