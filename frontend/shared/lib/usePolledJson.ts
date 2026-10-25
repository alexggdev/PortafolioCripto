import { useEffect, useRef, useState } from "react";

/**
 * Pide una URL JSON al cargar (si cambia la URL) y después cada cierto tiempo mientras la pestaña está visible.
 * Si la API responde 401 (sesión caducada) llama a `onUnauthorized`.
 * Si falla, conserva los últimos datos y lo indica en `failing`; `loading` es true mientras llega un cambio de URL.
 */
export function usePolledJson<T>(
  url: string,
  initial: T,
  intervalMs: number,
  options: { onUnauthorized?: () => void; fetchOnMount?: boolean } = {},
) {
  const optionsRef = useRef(options);
  optionsRef.current = options;
  const [data, setData] = useState<T>(initial);
  const [updatedAt, setUpdatedAt] = useState(() => Date.now());
  const [failing, setFailing] = useState(false);
  const [loading, setLoading] = useState(false);
  const firstRun = useRef(true);

  useEffect(() => {
    let stopped = false;
    const controller = new AbortController();

    async function load(showLoading: boolean) {
      if (showLoading) setLoading(true);
      try {
        const response = await fetch(url, { cache: "no-store", signal: controller.signal });
        if (response.status === 401) optionsRef.current.onUnauthorized?.();
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const next = (await response.json()) as T;
        if (stopped) return;
        setData(next);
        setFailing(false);
        setUpdatedAt(Date.now());
      } catch {
        if (!stopped) setFailing(true);
      } finally {
        if (!stopped) setLoading(false);
      }
    }

    // La primera vez ya hay datos del servidor; solo se vuelve a pedir al cambiar la URL
    // (o desde el principio si el servidor no pudo darlos: fetchOnMount).
    if (firstRun.current) {
      firstRun.current = false;
      if (optionsRef.current.fetchOnMount) void load(true);
    } else void load(true);

    const id = setInterval(() => {
      if (document.visibilityState === "visible") void load(false);
    }, intervalMs);
    const onVisible = () => {
      if (document.visibilityState === "visible") void load(false);
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      stopped = true;
      controller.abort();
      clearInterval(id);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [url, intervalMs]);

  return { data, updatedAt, failing, loading };
}
