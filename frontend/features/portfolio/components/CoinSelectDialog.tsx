"use client";

import { useId, useState } from "react";
import { ChevronRightIcon } from "@/shared/components/Icons";
import { Modal } from "@/shared/components/Modal";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import type { PickedCoin } from "@/features/portfolio/components/CoinPicker";
import { ROW_HOVER, SKELETON } from "@/shared/lib/ui";
import { useCoinSearch } from "@/features/portfolio/lib/useCoinSearch";
import type { AssetSummary } from "@/shared/lib/types";

export function CoinSkeletonRows({ count = 8 }: { count?: number }) {
  return (
    <>
      {Array.from({ length: count }, (_, i) => (
        <li key={i} aria-hidden="true" className="flex items-center gap-3 px-3 py-3">
          <span className={`size-[34px] rounded-full ${SKELETON}`} />
          <span className={`h-4 w-40 ${SKELETON}`} />
        </li>
      ))}
    </>
  );
}

/** Primer paso de «Añadir transacción» (solo desde el botón general): elegir la moneda. */
export function CoinSelectDialog({ onPick, onClose }: { onPick: (coin: PickedCoin) => void; onClose: () => void }) {
  const titleId = useId();
  const [query, setQuery] = useState("");
  const { results, loading } = useCoinSearch(query);

  const pick = (a: AssetSummary) => onPick({ slug: a.slug, symbol: a.symbol, name: a.name, imageUrl: a.imageUrl });

  return (
    <Modal labelId={titleId} onClose={onClose} width="30rem">
      <h2 id={titleId} className="pr-10 text-2xl font-bold">
        Seleccionar moneda
      </h2>
      <input
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter" && results[0]) {
            event.preventDefault();
            pick(results[0]);
          }
        }}
        maxLength={50}
        autoComplete="off"
        autoFocus
        placeholder="Buscar"
        aria-label="Buscar moneda"
        className="mt-4 w-full rounded-xl bg-rule px-4 py-3 text-ink transition-colors placeholder:text-soft hover:bg-ink/10 focus:bg-ink/10"
      />
      <ul aria-busy={loading} className="scroll-clean mt-3 h-[min(26rem,calc(90dvh-12rem))] overflow-y-auto">
        {loading && results.length === 0 && <CoinSkeletonRows />}
        {!loading && results.length === 0 && <li className="px-3 py-3 text-soft">Sin resultados.</li>}
        {results.map((asset) => (
          <li key={asset.id}>
            <button type="button" onClick={() => pick(asset)} className={`flex w-full cursor-pointer items-center gap-3 rounded-xl px-3 py-3 text-left ${ROW_HOVER}`}>
              <CoinIcon url={asset.imageUrl} size={34} />
              <span className="font-semibold">
                {asset.name} <span className="ml-1">{asset.symbol.toUpperCase()}</span>
              </span>
              <ChevronRightIcon className="ml-auto size-5 text-soft" />
            </button>
          </li>
        ))}
      </ul>
    </Modal>
  );
}
