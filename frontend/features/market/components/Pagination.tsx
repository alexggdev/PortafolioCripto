import Link from "next/link";
import { toPageHref, type ListQuery } from "@/features/market/lib/query";

export function Pagination({ query, totalPages }: { query: ListQuery; totalPages: number }) {
  if (totalPages <= 1) return null;

  const hasPrevious = query.page > 1;
  const hasNext = query.page < totalPages;
  const linkClass = "px-3 py-2 font-medium text-accent underline underline-offset-4";
  const disabledClass = "px-3 py-2 text-soft";

  return (
    <nav aria-label="Paginación" className="mt-4 flex items-center justify-between text-sm sm:text-base">
      {hasPrevious ? (
        <Link href={toPageHref(query, { page: query.page - 1 })} rel="prev" className={linkClass}>
          Anterior
        </Link>
      ) : (
        <span className={disabledClass}>Anterior</span>
      )}

      <p className="text-soft">
        Página {query.page} de {totalPages}
      </p>

      {hasNext ? (
        <Link href={toPageHref(query, { page: query.page + 1 })} rel="next" className={linkClass}>
          Siguiente
        </Link>
      ) : (
        <span className={disabledClass}>Siguiente</span>
      )}
    </nav>
  );
}
