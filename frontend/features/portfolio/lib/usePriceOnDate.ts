import { useEffect, useState } from "react";
import { priceOnDate } from "./priceOnDate";
import { localToUtcIso } from "./transactions";
import type { AssetSummary, AssetChart, ChartPoint } from "../../../shared/lib/types";

export type PriceState =
  | { status: "idle" }
  | { status: "loading" }
  /** `live`: la fecha es de hoy, así que es el precio de ahora; si no, el cierre de `date`. */
  | { status: "found"; priceUsd: number; date: string; live: boolean }
  | { status: "before"; firstDate: string }
  | { status: "error" };

const DAY_MS = 86_400_000;
const histories = new Map<string, ChartPoint[]>();

async function historyOf(slug: string, signal: AbortSignal): Promise<ChartPoint[]> {
  const cached = histories.get(slug);
  if (cached) return cached;
  const response = await fetch(`/api/assets/${encodeURIComponent(slug)}/chart?range=All`, { cache: "no-store", signal });
  if (!response.ok) throw new Error(String(response.status));
  const points = ((await response.json()) as AssetChart).points;
  histories.set(slug, points);
  return points;
}

/**
 * Precio de una moneda en la fecha elegida (hora local «AAAA-MM-DDTHH:mm»): el cierre de ese día, o el precio de ahora
 * si es de hoy. También avisa («before») si la moneda todavía no tenía datos ese día.
 */
export function usePriceOnDate(slug: string | null, local: string): PriceState {
  const [state, setState] = useState<PriceState>({ status: "idle" });

  useEffect(() => {
    const iso = localToUtcIso(local);
    if (!slug || !iso) {
      setState({ status: "idle" });
      return;
    }
    const controller = new AbortController();
    setState({ status: "loading" });

    (async () => {
      try {
        if (Date.parse(iso) >= Date.now() - DAY_MS) {
          const response = await fetch(`/api/assets/${encodeURIComponent(slug)}`, { cache: "no-store", signal: controller.signal });
          if (!response.ok) throw new Error(String(response.status));
          const asset = (await response.json()) as AssetSummary;
          if (asset.priceUsd === null) throw new Error("sin precio");
          setState({ status: "found", priceUsd: asset.priceUsd, date: iso.slice(0, 10), live: true });
          return;
        }
        const result = priceOnDate(await historyOf(slug, controller.signal), iso);
        if (result.status === "found") setState({ status: "found", priceUsd: result.priceUsd, date: result.date, live: false });
        else if (result.status === "before") setState({ status: "before", firstDate: result.firstDate });
        else setState({ status: "error" });
      } catch {
        if (!controller.signal.aborted) setState({ status: "error" });
      }
    })();

    return () => controller.abort();
  }, [slug, local]);

  return state;
}
