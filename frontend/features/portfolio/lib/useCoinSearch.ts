"use client";

import { useEffect, useState } from "react";
import { cachedTopCoins, prefetchTopCoins, searchCoins } from "./coinSearch";
import type { AssetSummary } from "../../../shared/lib/types";

/**
 * Monedas que coinciden con lo escrito. Sin texto, las primeras del ranking (al instante si ya se pidieron).
 * Mientras llega la respuesta se conserva la lista anterior; un fallo se reintenta solo, nunca se muestra como error.
 */
export function useCoinSearch(query: string, enabled = true): { results: AssetSummary[]; loading: boolean } {
  const [results, setResults] = useState<AssetSummary[]>(() => cachedTopCoins() ?? []);
  const [loading, setLoading] = useState(() => cachedTopCoins() === null);

  useEffect(() => {
    if (!enabled) return;
    const text = query.trim();
    const cached = text ? null : cachedTopCoins();
    if (cached) {
      setResults(cached);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    const load = async (attempt: number) => {
      setLoading(true);
      try {
        const items = text ? await searchCoins(text, controller.signal) : await prefetchTopCoins();
        if (controller.signal.aborted) return;
        setResults(items);
        setLoading(false);
      } catch {
        if (!controller.signal.aborted) timer = setTimeout(() => void load(attempt + 1), Math.min(500 * 2 ** attempt, 5000));
      }
    };
    // La lista inicial se pide sin esperar; al escribir, se espera un momento a que termine de teclear.
    timer = setTimeout(() => void load(0), text ? 200 : 0);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, enabled]);

  return { results, loading };
}
