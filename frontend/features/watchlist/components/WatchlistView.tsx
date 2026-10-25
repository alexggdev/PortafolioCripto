"use client";

import Link from "next/link";
import { useState } from "react";
import { CurrencyNote } from "@/features/currency/components/CurrencyNote";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { PctText } from "@/shared/components/PctText";
import { UpdatedAgo } from "@/features/market/components/UpdatedAgo";
import { WatchStar } from "@/features/watchlist/components/WatchStar";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import type { WatchlistItem } from "@/shared/lib/types";
import { ROW_HOVER } from "@/shared/lib/ui";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 10_000;

/** Monedas que sigue el usuario, con precio que se refresca solo. */
export function WatchlistView({ initial }: { initial: WatchlistItem[] }) {
  const money = useMoney();
  const { data, updatedAt, failing } = usePolledJson<WatchlistItem[]>("/api/watchlist", initial, POLL_MS, {
    onUnauthorized: redirectToLogin,
  });
  // Las que se acaban de quitar desaparecen al instante, sin esperar a la siguiente lectura.
  const [removed, setRemoved] = useState<Set<string>>(() => new Set());
  const items = data.filter((item) => !removed.has(item.asset.slug));

  if (items.length === 0) {
    return (
      <div className="mt-8 bg-surface px-4 py-12">
        <p className="font-medium">Todavía no sigues ninguna moneda.</p>
        <p className="mt-1 text-soft">Pulsa la estrella de una moneda en el mercado para tenerla aquí.</p>
        <Link href="/" className="mt-4 inline-block font-medium text-accent underline underline-offset-4">
          Ir al mercado
        </Link>
      </div>
    );
  }

  return (
    <div className="mt-8">
      <div className="mb-3 flex justify-end text-sm">
        <UpdatedAgo updatedAt={updatedAt} failing={failing} />
      </div>
      <div className="overflow-x-auto bg-surface">
        <table className="w-full border-collapse text-sm sm:text-base">
          <caption className="sr-only">Monedas que sigues</caption>
          <thead>
            <tr className="border-b border-rule text-left text-soft">
              <th scope="col" className="w-10 px-1 py-3">
                <span className="sr-only">Dejar de seguir</span>
              </th>
              <th scope="col" className="px-2 py-3 font-medium sm:px-3">Moneda</th>
              <th scope="col" className="px-2 py-3 text-right font-medium sm:px-3">Precio</th>
              <th scope="col" className="px-2 py-3 text-right font-medium sm:px-3">24 h</th>
              <th scope="col" className="hidden px-2 py-3 text-right font-medium sm:px-3 md:table-cell">7 d</th>
              <th scope="col" className="hidden px-2 py-3 text-right font-medium sm:px-3 lg:table-cell">Capitalización</th>
            </tr>
          </thead>
          <tbody>
            {items.map(({ asset }) => (
              <tr key={asset.id} className={`border-b border-rule last:border-b-0 ${ROW_HOVER}`}>
                <td className="px-1 py-3">
                  <WatchStar
                    slug={asset.slug}
                    name={asset.name}
                    initialWatched
                    signedIn
                    onChange={(watched) => {
                      if (!watched) setRemoved((prev) => new Set(prev).add(asset.slug));
                    }}
                  />
                </td>
                <td className="px-2 py-3 sm:px-3">
                  <Link href={`/monedas/${asset.slug}`} className="flex items-center gap-3">
                    {asset.imageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img src={asset.imageUrl} alt="" width={24} height={24} loading="lazy" referrerPolicy="no-referrer" className="size-5 rounded-full sm:size-6" />
                    ) : (
                      <span aria-hidden="true" className="inline-block size-5 rounded-full bg-rule sm:size-6" />
                    )}
                    <span className="min-w-0">
                      <span className="condensed block truncate text-[1.05em] font-semibold leading-tight">{asset.name}</span>
                      <span className="block text-xs text-soft">{asset.symbol}</span>
                    </span>
                  </Link>
                </td>
                <td className="px-2 py-3 text-right font-medium tabular-nums sm:px-3">{money.price(asset.priceUsd)}</td>
                <td className="px-2 py-3 text-right sm:px-3"><PctText value={asset.change24hPct} /></td>
                <td className="hidden px-2 py-3 text-right sm:px-3 md:table-cell"><PctText value={asset.change7dPct} /></td>
                <td className="hidden px-2 py-3 text-right tabular-nums sm:px-3 lg:table-cell">{money.whole(asset.marketCapUsd)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <CurrencyNote />
    </div>
  );
}
