"use client";

import { useEffect, useId, useState } from "react";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import type { AssetSummary, PagedResult } from "@/shared/lib/types";

export interface PickedCoin {
  slug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
}

const SHOWN = 8;

function CoinImage({ url }: { url: string | null }) {
  // eslint-disable-next-line @next/next/no-img-element
  return url ? <img src={url} alt="" width={20} height={20} className="h-5 w-5 rounded-full" /> : <span aria-hidden="true" className="h-5 w-5 rounded-full bg-rule" />;
}

/** Buscador de monedas del catálogo. Sin texto muestra las primeras del ranking. */
export function CoinPicker({
  value,
  onChange,
  invalid,
  describedBy,
}: {
  value: PickedCoin | null;
  onChange: (coin: PickedCoin | null) => void;
  invalid?: boolean;
  describedBy?: string;
}) {
  const id = useId();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<AssetSummary[]>([]);
  const [state, setState] = useState<"idle" | "loading" | "error">("idle");

  useEffect(() => {
    if (value) return;
    const controller = new AbortController();
    const timer = setTimeout(async () => {
      setState("loading");
      try {
        const params = new URLSearchParams();
        if (query.trim()) params.set("search", query.trim().slice(0, 50));
        const response = await fetch(`/api/assets?${params}`, { signal: controller.signal, cache: "no-store" });
        if (response.status === 401) redirectToLogin();
        if (!response.ok) throw new Error(String(response.status));
        const data = (await response.json()) as PagedResult<AssetSummary>;
        setResults(data.items.slice(0, SHOWN));
        setState("idle");
      } catch {
        if (!controller.signal.aborted) setState("error");
      }
    }, 250);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, value]);

  function pick(asset: AssetSummary) {
    onChange({ slug: asset.slug, symbol: asset.symbol, name: asset.name, imageUrl: asset.imageUrl });
    setQuery("");
  }

  if (value) {
    return (
      <div className="flex items-center justify-between gap-3 border border-rule bg-surface px-3 py-2">
        <span className="flex items-center gap-2">
          <CoinImage url={value.imageUrl} />
          <span className="font-medium">{value.name}</span>
          <span className="text-sm text-soft">{value.symbol.toUpperCase()}</span>
        </span>
        <button type="button" onClick={() => onChange(null)} className="text-sm underline underline-offset-4">
          Cambiar
        </button>
      </div>
    );
  }

  return (
    <div>
      <input
        id={id}
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault(); // Enter elige el primer resultado en vez de enviar el formulario
            if (results[0]) pick(results[0]);
          }
        }}
        maxLength={50}
        autoComplete="off"
        placeholder="Busca por nombre o símbolo"
        aria-label="Buscar moneda"
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
        className="w-full border border-rule bg-surface px-3 py-2 text-ink placeholder:text-soft"
      />
      <ul className="mt-1 max-h-48 overflow-y-auto border border-rule bg-surface" aria-busy={state === "loading"}>
        {state === "error" && <li className="px-3 py-2 text-sm text-down">No se pudo buscar. Inténtalo de nuevo.</li>}
        {state !== "error" && results.length === 0 && state === "idle" && (
          <li className="px-3 py-2 text-sm text-soft">Sin resultados.</li>
        )}
        {results.map((asset) => (
          <li key={asset.id}>
            <button
              type="button"
              onClick={() => pick(asset)}
              className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-paper"
            >
              <CoinImage url={asset.imageUrl} />
              <span className="font-medium">{asset.name}</span>
              <span className="text-sm text-soft">{asset.symbol.toUpperCase()}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
