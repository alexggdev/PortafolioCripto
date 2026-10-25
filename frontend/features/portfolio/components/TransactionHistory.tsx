"use client";

import { useEffect, useState } from "react";
import { ConfirmDialog } from "@/shared/components/ConfirmDialog";
import { TransactionDetailsDialog } from "@/features/portfolio/components/TransactionDetailsDialog";
import { TransactionsTable } from "@/features/portfolio/components/TransactionsTable";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { portfolioQuery } from "@/features/portfolio/lib/portfolioId";
import type { PagedResult, TransactionDto, TransactionType } from "@/shared/lib/types";

// Lo último que se vio de cada lista: al volver a la pestaña se muestra al instante y se actualiza por detrás.
const cache = new Map<string, PagedResult<TransactionDto>>();
const CACHE_LIMIT = 40;

/** Filas inventadas para dibujar la tabla borrosa mientras llegan las de verdad. */
const PLACEHOLDER: TransactionDto[] = Array.from({ length: 8 }, (_, i) => ({
  id: -(i + 1),
  assetSlug: "bitcoin",
  symbol: "BTC",
  name: "Bitcoin",
  imageUrl: null,
  type: i % 3 === 2 ? "Sell" : "Buy",
  quantity: 1.25,
  unitPriceUsd: 64000,
  feeUsd: i % 2 === 0 ? 0 : 12,
  dateUtc: new Date(Date.UTC(2026, 9, 20 - i * 9)).toISOString(),
  note: null,
}));

const noop = () => undefined;

function listUrl(page: number, pageSize: number, slug: string | null, type: TransactionType | null, portfolioId: string | null): string {
  const assetParam = slug ? `&assetSlug=${encodeURIComponent(slug)}` : "";
  const typeParam = type ? `&type=${type}` : "";
  return `/api/portfolio/transactions?page=${page}&pageSize=${pageSize}${assetParam}${typeParam}${portfolioQuery(portfolioId)}`;
}

/**
 * Transacciones de un portafolio (filtradas por moneda y tipo si se pide), con paginación, detalle al pulsar una fila
 * y aviso de confirmación antes de borrar.
 */
export function TransactionHistory({
  portfolioId,
  version,
  onEdit,
  onChanged,
  slug = null,
  type = null,
  showCoin = true,
  pageSize = 20,
}: {
  portfolioId: string | null;
  /** Al cambiar, se vuelven a pedir los datos. */
  version: number;
  onEdit: (tx: TransactionDto) => void;
  onChanged: () => void;
  /** Solo las transacciones de esta moneda. */
  slug?: string | null;
  /** Solo las de este tipo. */
  type?: TransactionType | null;
  /** Columna «Activos»: sobra en la página de una moneda. */
  showCoin?: boolean;
  /** Transacciones por página (la página de una moneda pide hasta 100 para verlas todas). */
  pageSize?: number;
}) {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<TransactionDto> | null>(() => cache.get(listUrl(1, pageSize, slug, type, portfolioId)) ?? null);
  const [details, setDetails] = useState<TransactionDto | null>(null);
  const [deleting, setDeleting] = useState<TransactionDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [deleteErrors, setDeleteErrors] = useState<string[]>([]);

  // Si el servidor tarda o falla, se reintenta solo con pausas cada vez mayores: se sigue viendo la tabla (borrosa o con
  // lo último que había) y nunca se muestra un error.
  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;

    const load = async (attempt: number) => {
      try {
        const url = listUrl(page, pageSize, slug, type, portfolioId);
        const response = await fetch(url, { cache: "no-store", signal: controller.signal });
        if (response.status === 401) {
          redirectToLogin();
          return;
        }
        if (!response.ok) throw new Error(String(response.status));
        const next = (await response.json()) as PagedResult<TransactionDto>;
        // Al borrar la última de una página, se vuelve a la anterior.
        if (next.items.length === 0 && page > 1) setPage(page - 1);
        else {
          if (cache.size >= CACHE_LIMIT) cache.clear();
          cache.set(url, next);
          setData(next);
        }
      } catch {
        if (!controller.signal.aborted) timer = setTimeout(() => void load(attempt + 1), Math.min(800 * 2 ** attempt, 5000));
      }
    };

    void load(0);
    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [page, version, portfolioId, slug, type, pageSize]);

  // Otro filtro: se vuelve a la primera página.
  useEffect(() => setPage(1), [slug, type]);

  async function confirmDelete() {
    if (!deleting) return;
    setBusy(true);
    setDeleteErrors([]);
    try {
      const response = await fetch(`/api/portfolio/transactions/${deleting.id}`, { method: "DELETE" });
      if (response.status === 401) {
        redirectToLogin();
        return;
      }
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { error?: string } | null;
        setDeleteErrors([body?.error ?? "No se pudo borrar la transacción."]);
        return;
      }
      setDeleting(null);
      onChanged();
    } catch {
      setDeleteErrors(["No se pudo conectar con el servidor. Inténtalo de nuevo."]);
    } finally {
      setBusy(false);
    }
  }

  if (!data) {
    return (
      <div role="status" aria-label="Cargando transacciones">
        <TransactionsTable rows={PLACEHOLDER} showCoin={showCoin} loading onOpen={noop} onEdit={noop} onDelete={noop} />
      </div>
    );
  }

  if (data.totalCount === 0) {
    return <p className="px-3 py-4 text-sm text-soft">{slug || type ? "Ninguna transacción coincide con el filtro." : "Aún no hay transacciones."}</p>;
  }

  return (
    <div className="space-y-3">
      <TransactionsTable
        rows={data.items}
        showCoin={showCoin}
        onOpen={setDetails}
        onEdit={onEdit}
        onDelete={(tx) => {
          setDeleteErrors([]);
          setDeleting(tx);
        }}
      />

      {data.totalPages > 1 && (
        <nav aria-label="Páginas de transacciones" className="flex items-center justify-between px-3 pb-2 text-sm">
          <button type="button" onClick={() => setPage(page - 1)} disabled={page <= 1} className="rounded-lg bg-rule px-3 py-1.5 font-medium disabled:opacity-50">
            Anterior
          </button>
          <span className="text-soft">
            Página {data.page} de {data.totalPages}
          </span>
          <button type="button" onClick={() => setPage(page + 1)} disabled={page >= data.totalPages} className="rounded-lg bg-rule px-3 py-1.5 font-medium disabled:opacity-50">
            Siguiente
          </button>
        </nav>
      )}

      {details && <TransactionDetailsDialog tx={details} onClose={() => setDetails(null)} />}
      {deleting && (
        <ConfirmDialog
          title="Eliminar transacción"
          message="¿Seguro que quieres eliminar esta transacción?"
          confirmLabel="Eliminar"
          busy={busy}
          errors={deleteErrors}
          onConfirm={confirmDelete}
          onCancel={() => setDeleting(null)}
        />
      )}
    </div>
  );
}
