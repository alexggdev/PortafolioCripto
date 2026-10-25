"use client";

import { useCallback, useState } from "react";
import { CheckIcon, ChevronDownIcon, SearchIcon } from "@/shared/components/Icons";
import { PopPanel } from "@/shared/components/PopMenu";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { CoinSkeletonRows } from "@/features/portfolio/components/CoinSelectDialog";
import type { PickedCoin } from "@/features/portfolio/components/CoinPicker";
import { useCoinSearch } from "@/features/portfolio/lib/useCoinSearch";
import { ROW_HOVER } from "@/shared/lib/ui";

/** Campo desplegable con la moneda elegida: al pulsarlo se abre una lista con buscador, sin salir de la ventana. */
export function CoinCombobox({ coin, onPick, className }: { coin: PickedCoin; onPick: (coin: PickedCoin) => void; className: string }) {
  const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);
  const [query, setQuery] = useState("");
  const { results, loading } = useCoinSearch(query, anchor !== null);
  const close = useCallback(() => {
    setAnchor(null);
    setQuery("");
  }, []);

  return (
    <>
      <button
        type="button"
        onClick={(event) => {
          const button = event.currentTarget;
          setAnchor((current) => (current ? null : button));
        }}
        aria-haspopup="listbox"
        aria-expanded={anchor !== null}
        aria-label={`Moneda: ${coin.name}`}
        className={`${className} flex cursor-pointer items-center gap-3 text-left`}
      >
        <CoinIcon url={coin.imageUrl} size={24} />
        <span className="font-medium">
          {coin.name} ({coin.symbol.toUpperCase()})
        </span>
        <ChevronDownIcon className={`ml-auto size-4 text-soft transition-transform ${anchor ? "rotate-180" : ""}`} />
      </button>
      {anchor && (
        <PopPanel anchor={anchor} matchWidth autoFocus={false} label="Monedas" onClose={close} className="overflow-hidden p-0 shadow-2xl">
          <div className="flex items-center gap-2 border-b border-rule px-4 py-3">
            <SearchIcon className="size-4 shrink-0 text-soft" />
            <input
              autoFocus
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && results[0]) {
                  event.preventDefault();
                  onPick({ slug: results[0].slug, symbol: results[0].symbol, name: results[0].name, imageUrl: results[0].imageUrl });
                  close();
                }
              }}
              maxLength={50}
              autoComplete="off"
              placeholder="Buscar"
              aria-label="Buscar moneda"
              className="w-full min-w-0 bg-transparent text-ink outline-none placeholder:text-soft"
            />
          </div>
          <ul role="listbox" aria-label="Monedas" aria-busy={loading} className="scrollbar-slim max-h-60 overflow-y-auto p-2">
            {loading && results.length === 0 && <CoinSkeletonRows count={5} />}
            {!loading && results.length === 0 && <li className="px-3 py-2 text-sm text-soft">Sin resultados.</li>}
            {results.map((asset) => (
              <li key={asset.id} role="option" aria-selected={asset.slug === coin.slug}>
                <button
                  type="button"
                  onClick={() => {
                    onPick({ slug: asset.slug, symbol: asset.symbol, name: asset.name, imageUrl: asset.imageUrl });
                    close();
                  }}
                  className={`flex w-full cursor-pointer items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm ${ROW_HOVER}`}
                >
                  <CoinIcon url={asset.imageUrl} size={24} />
                  <span className="font-medium">
                    {asset.name} <span className="text-soft">{asset.symbol.toUpperCase()}</span>
                  </span>
                  {asset.slug === coin.slug && <CheckIcon className="ml-auto size-4 text-accent" />}
                </button>
              </li>
            ))}
          </ul>
        </PopPanel>
      )}
    </>
  );
}
