import { useEffect, useRef, useState } from "react";
import { redirectToLogin } from "../../auth/lib/clientAuth";
import { sumHistories } from "./portfolioHistory";
import { portfolioQuery } from "./portfolioId";
import type { PortfolioHistory, RangeCode } from "../../../shared/lib/types";

const POLL_MS = 60_000;

/**
 * Historia diaria de uno o varios portafolios (varios = se suman, para la vista general).
 * `loading` es true mientras llega un cambio de periodo o de datos; las actualizaciones periódicas son silenciosas.
 * `initial` evita la primera petición cuando el servidor ya mandó la historia del mismo periodo.
 */
export function usePortfolioHistory(
  ids: readonly (string | null)[],
  range: RangeCode,
  version: number,
  initial: PortfolioHistory | null = null,
) {
  const [data, setData] = useState<PortfolioHistory | null>(initial);
  const [loading, setLoading] = useState(initial === null);
  const [failing, setFailing] = useState(false);
  const skipFirst = useRef(initial !== null && initial.range === range && ids.length === 1);
  const key = `${ids.map((id) => id ?? "").join(",")}|${range}|${version}`;

  useEffect(() => {
    let stopped = false;
    const controller = new AbortController();

    async function load(showLoading: boolean) {
      if (showLoading) setLoading(true);
      try {
        const responses = await Promise.all(
          ids.map((id) =>
            fetch(`/api/portfolio/history?range=${range}&v=${version}${portfolioQuery(id)}`, { cache: "no-store", signal: controller.signal }),
          ),
        );
        if (responses.some((r) => r.status === 401)) {
          redirectToLogin();
          return;
        }
        if (responses.some((r) => !r.ok)) throw new Error("http");
        const histories = (await Promise.all(responses.map((r) => r.json()))) as PortfolioHistory[];
        if (stopped) return;
        setData(histories.length === 1 ? histories[0] : sumHistories(histories));
        setFailing(false);
      } catch {
        if (!stopped && !controller.signal.aborted) setFailing(true);
      } finally {
        if (!stopped) setLoading(false);
      }
    }

    if (skipFirst.current) skipFirst.current = false;
    else void load(true);

    const timer = setInterval(() => {
      if (document.visibilityState === "visible") void load(false);
    }, POLL_MS);
    return () => {
      stopped = true;
      controller.abort();
      clearInterval(timer);
    };
    // `ids` se resume en `key`: un array nuevo con los mismos ids no debe repetir la petición.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return { data, loading, failing, key };
}
