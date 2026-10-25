import { SKELETON } from "@/shared/lib/ui";

const BAR = SKELETON;

function SideRow() {
  return (
    <div className="flex items-center gap-3 px-2.5 py-2">
      <span className={`size-9 shrink-0 rounded-xl ${BAR}`} />
      <span className="flex-1 space-y-2">
        <span className={`block h-3.5 w-28 ${BAR}`} />
        <span className={`block h-3 w-16 ${BAR}`} />
      </span>
    </div>
  );
}

function Card({ className = "" }: { className?: string }) {
  return (
    <div className={`rounded-2xl bg-surface p-4 ${className}`}>
      <span className={`block h-3.5 w-28 ${BAR}`} />
      <span className={`mt-3 block h-7 w-40 blur-[3px] ${BAR}`} />
    </div>
  );
}

/** Esqueleto de las páginas del portafolio: la misma forma que la página real, con bloques que parpadean (nunca texto). */
export function PortfolioSkeleton() {
  return (
    <div role="status" aria-busy="true" aria-label="Cargando" className="grid gap-4 pt-1 lg:grid-cols-[19rem_minmax(0,1fr)] lg:gap-6">
      <aside className="hidden space-y-1 lg:block lg:border-r lg:border-rule lg:pr-4">
        <SideRow />
        <span className={`my-3 ml-1 block h-4 w-32 ${BAR}`} />
        <SideRow />
        <SideRow />
        <SideRow />
      </aside>

      <div className="min-w-0 space-y-3">
        <div className="flex items-center gap-4">
          <span className={`size-12 shrink-0 rounded-xl ${BAR}`} />
          <div className="space-y-2">
            <span className={`block h-3.5 w-24 ${BAR}`} />
            <span className={`block h-9 w-56 blur-[3px] ${BAR}`} />
          </div>
        </div>

        <div className="grid gap-3 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
          <Card className="h-72" />
          <Card className="h-72" />
          <div className="grid gap-3 sm:grid-cols-2">
            <Card />
            <Card />
          </div>
          <Card />
        </div>

        <div className="rounded-2xl bg-surface p-4">
          <div className="space-y-4">
            {Array.from({ length: 5 }, (_, i) => (
              <div key={i} className="flex items-center gap-3">
                <span className={`size-8 shrink-0 rounded-full ${BAR}`} />
                <span className={`h-4 flex-1 blur-[2px] ${BAR}`} />
                <span className={`h-4 w-24 blur-[2px] ${BAR}`} />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
