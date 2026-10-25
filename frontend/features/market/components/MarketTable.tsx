"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { ChangeCell } from "@/features/market/components/ChangeCell";
import { UpdatedAgo } from "@/features/market/components/UpdatedAgo";
import { WatchStar } from "@/features/watchlist/components/WatchStar";
import { useCurrency, useMoney } from "@/features/currency/components/CurrencyProvider";
import { NO_VALUE } from "@/shared/lib/format";
import { CURRENCY_NAMES } from "@/features/currency/lib/currency";
import { nextSort, toPageHref, toSearchParams, type ListQuery } from "@/features/market/lib/query";
import type { AssetSummary, PagedResult, SortKey } from "@/shared/lib/types";
import { ROW_HOVER } from "@/shared/lib/ui";

const POLL_MS = 10_000;

interface Column {
  key: SortKey;
  label: string;
  align: "left" | "right";
  className?: string;
}

const COLUMNS: Column[] = [
  { key: "rank", label: "#", align: "left", className: "hidden w-12 sm:table-cell" },
  { key: "name", label: "Moneda", align: "left" },
  { key: "price", label: "Precio", align: "right" },
  { key: "change24h", label: "24 h", align: "right", className: "hidden sm:table-cell" },
  { key: "marketCap", label: "Capitalización", align: "right", className: "hidden lg:table-cell" },
  { key: "volume", label: "Volumen 24 h", align: "right", className: "hidden xl:table-cell" },
];

type Flash = { dir: "up" | "down"; n: number };

function SortMark({ active, desc }: { active: boolean; desc: boolean }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 8 10"
      className={`ml-1 inline-block h-2.5 w-2 ${active ? "text-accent" : "text-rule"}`}
      fill="currentColor"
    >
      {(!active || !desc) && <path d="M4 0 8 4H0z" />}
      {(!active || desc) && <path d="M4 10 0 6h8z" />}
    </svg>
  );
}

export function MarketTable({
  initial,
  query,
  watched = [],
  signedIn = false,
}: {
  initial: PagedResult<AssetSummary>;
  query: ListQuery;
  /** Slugs que sigue el usuario. */
  watched?: string[];
  signedIn?: boolean;
}) {
  const watchedSet = new Set(watched);
  const { currency } = useCurrency();
  const [data, setData] = useState(initial);
  const [updatedAt, setUpdatedAt] = useState(() => Date.now());
  const [failing, setFailing] = useState(false);
  const [flashes, setFlashes] = useState<Record<number, Flash>>({});
  const lastPrices = useRef(new Map<number, number | null>(initial.items.map((i) => [i.id, i.priceUsd])));

  const queryString = toSearchParams(query).toString();

  useEffect(() => {
    let stopped = false;
    const controller = new AbortController();

    async function refresh() {
      if (document.visibilityState !== "visible") return;
      try {
        const response = await fetch(`/api/assets${queryString ? `?${queryString}` : ""}`, {
          cache: "no-store",
          signal: controller.signal,
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const next = (await response.json()) as PagedResult<AssetSummary>;
        if (stopped) return;

        const changed: Record<number, Flash> = {};
        for (const item of next.items) {
          const previous = lastPrices.current.get(item.id);
          if (previous != null && item.priceUsd != null && previous !== item.priceUsd) {
            changed[item.id] = { dir: item.priceUsd > previous ? "up" : "down", n: Date.now() };
          }
          lastPrices.current.set(item.id, item.priceUsd);
        }

        setFlashes(changed);
        setData(next);
        setFailing(false);
        setUpdatedAt(Date.now());
      } catch {
        if (!stopped) setFailing(true); // se conservan los últimos precios en pantalla
      }
    }

    const id = setInterval(refresh, POLL_MS);
    const onVisible = () => {
      if (document.visibilityState === "visible") void refresh();
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      stopped = true;
      controller.abort();
      clearInterval(id);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [queryString]);

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-x-6 gap-y-1 text-sm">
        <ul className="flex items-center gap-x-5 text-soft">
          <li className="flex items-center gap-1.5">
            <span aria-hidden="true" className="inline-block size-2 rounded-full bg-ink" />
            Precio en vivo
          </li>
          <li className="flex items-center gap-1.5">
            <span aria-hidden="true" className="inline-block size-2 rounded-full border border-ink" />
            Precio con retraso
          </li>
        </ul>
        <UpdatedAgo updatedAt={updatedAt} failing={failing} />
      </div>

      <div className="overflow-x-auto bg-surface">
        <table className="w-full border-collapse text-sm sm:text-base">
          <caption className="sr-only">
            Precios de criptomonedas en {CURRENCY_NAMES[currency].plural}, ordenados por {query.sort === "rank" ? "ranking" : (COLUMNS.find((c) => c.key === query.sort)?.label ?? "ranking")} de forma{" "}
            {query.desc ? "descendente" : "ascendente"}
          </caption>
          <thead>
            <tr className="border-b border-rule text-left text-soft">
              <th scope="col" className="w-10 px-1 py-3">
                <span className="sr-only">Seguir</span>
              </th>
              {COLUMNS.map((column) => {
                const active = query.sort === column.key;
                return (
                  <th
                    key={column.key}
                    scope="col"
                    aria-sort={active ? (query.desc ? "descending" : "ascending") : undefined}
                    className={`px-2 py-3 sm:px-3 font-medium ${column.align === "right" ? "text-right" : "text-left"} ${column.className ?? ""}`}
                  >
                    <Link
                      href={toPageHref(query, nextSort(query, column.key))}
                      className="inline-flex items-center hover:text-ink"
                      scroll={false}
                    >
                      {column.label}
                      <SortMark active={active} desc={query.desc} />
                    </Link>
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {data.items.map((asset) => (
              <Row key={asset.id} asset={asset} flash={flashes[asset.id]} watched={watchedSet.has(asset.slug)} signedIn={signedIn} />
            ))}
          </tbody>
        </table>

        {data.items.length === 0 && <EmptyState query={query} />}
      </div>
    </div>
  );
}

function Row({
  asset,
  flash,
  watched,
  signedIn,
}: {
  asset: AssetSummary;
  flash: Flash | undefined;
  watched: boolean;
  signedIn: boolean;
}) {
  const money = useMoney();
  const delayed = asset.priceSource === "delayed";
  const delayText = "Precio con retraso: viene de CoinGecko y puede tener unos minutos";

  return (
    <tr className={`border-b border-rule last:border-b-0 ${ROW_HOVER}`}>
      <td className="px-1 py-3">
        <WatchStar slug={asset.slug} name={asset.name} initialWatched={watched} signedIn={signedIn} />
      </td>
      <td className="hidden px-2 py-3 sm:px-3 text-soft tabular-nums sm:table-cell">{asset.rank ?? NO_VALUE}</td>
      <td className="px-2 py-3 sm:px-3">
        <Link href={`/monedas/${asset.slug}`} className="flex items-center gap-3">
          {asset.imageUrl ? (
            // Imágenes remotas de CoinGecko: <img> directo para no configurar dominios de next/image.
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={asset.imageUrl}
              alt=""
              width={24}
              height={24}
              loading="lazy"
              referrerPolicy="no-referrer"
              className="size-5 rounded-full sm:size-6"
            />
          ) : (
            <span aria-hidden="true" className="inline-block size-5 rounded-full bg-rule sm:size-6" />
          )}
          <span className="min-w-0">
            <span className="condensed block truncate text-[1.05em] font-semibold leading-tight">{asset.name}</span>
            <span className="block text-xs text-soft">{asset.symbol}</span>
          </span>
        </Link>
      </td>
      <td className="px-2 py-3 sm:px-3 text-right tabular-nums">
        <span
          key={flash ? `${asset.id}-${flash.n}` : asset.id}
          className={`inline-flex items-center gap-1.5 px-1 ${flash ? (flash.dir === "up" ? "flash-up" : "flash-down") : ""}`}
        >
          {delayed && (
            <span
              role="img"
              aria-label={delayText}
              title={delayText}
              className="inline-block size-2 rounded-full border border-ink"
            />
          )}
          <span className="font-medium">{money.price(asset.priceUsd)}</span>
        </span>
        {/* En móvil no cabe la columna de 24 h: la variación va debajo del precio. */}
        <div className="mt-0.5 text-xs sm:hidden">
          <ChangeCell value={asset.change24hPct} />
        </div>
      </td>
      <td className="hidden px-2 py-3 sm:table-cell sm:px-3">
        <ChangeCell value={asset.change24hPct} />
      </td>
      <td className="hidden px-2 py-3 sm:px-3 text-right tabular-nums lg:table-cell">{money.whole(asset.marketCapUsd)}</td>
      <td className="hidden px-2 py-3 sm:px-3 text-right tabular-nums xl:table-cell">{money.whole(asset.volume24hUsd)}</td>
    </tr>
  );
}

function EmptyState({ query }: { query: ListQuery }) {
  const searching = query.search.length > 0;
  const beyondLastPage = !searching && query.page > 1;

  return (
    <div className="px-3 py-12">
      <p className="font-medium">
        {searching
          ? `Ninguna moneda coincide con «${query.search}».`
          : beyondLastPage
            ? "Esa página no existe."
            : "Todavía no hay monedas en el catálogo."}
      </p>
      <p className="mt-1 text-soft">
        {searching ? "Prueba con otro nombre o con el símbolo, por ejemplo BTC." : "Vuelve a la primera página del mercado."}
      </p>
      {(searching || beyondLastPage) && (
        <Link
          href={toPageHref(query, { search: "", page: 1 })}
          className="mt-4 inline-block font-medium text-accent underline underline-offset-4"
        >
          Ver todo el mercado
        </Link>
      )}
    </div>
  );
}
