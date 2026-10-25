"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { ChevronRightIcon, StarIcon } from "@/shared/components/Icons";
import { PctText } from "@/shared/components/PctText";
import { PopPanel } from "@/shared/components/PopMenu";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { formatCompactMoney, sparklinePath } from "@/shared/lib/compact";
import { directionOf } from "@/shared/lib/format";
import { NAV_LINK, ROW_HOVER, SKELETON } from "@/shared/lib/ui";
import type { AssetChart, WatchlistItem } from "@/shared/lib/types";

const REFRESH_MS = 15_000;
const SPARK_W = 84;
const SPARK_H = 30;
const COLOR = { up: "text-up", down: "text-down", flat: "text-soft" } as const;

// Línea de los últimos 7 días de cada moneda: se pide cuando la fila se ve y se recuerda.
const sparks = new Map<string, number[]>();

function Spark({ slug }: { slug: string }) {
  const ref = useRef<HTMLSpanElement>(null);
  const [values, setValues] = useState<number[] | null>(() => sparks.get(slug) ?? null);

  useEffect(() => {
    const element = ref.current;
    if (values || !element) return;
    const controller = new AbortController();
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some((e) => e.isIntersecting)) return;
      observer.disconnect();
      fetch(`/api/assets/${encodeURIComponent(slug)}/chart?range=7D`, { signal: controller.signal, cache: "no-store" })
        .then((response) => (response.ok ? (response.json() as Promise<AssetChart>) : null))
        .then((chart) => {
          const next = chart?.points.map((p) => p.priceUsd) ?? [];
          if (next.length > 1) {
            sparks.set(slug, next);
            setValues(next);
          }
        })
        .catch(() => undefined);
    });
    observer.observe(element);
    return () => {
      observer.disconnect();
      controller.abort();
    };
  }, [slug, values]);

  const path = values ? sparklinePath(values, SPARK_W, SPARK_H) : null;
  const tone = values ? COLOR[directionOf(values[values.length - 1] - values[0])] : "";
  return (
    <span ref={ref} aria-hidden="true" className="inline-flex" style={{ width: SPARK_W, height: SPARK_H }}>
      {path ? (
        <svg viewBox={`0 0 ${SPARK_W} ${SPARK_H}`} width={SPARK_W} height={SPARK_H} className={`fade-in ${tone}`} fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round" strokeLinecap="round">
          <path d={path} />
        </svg>
      ) : (
        <span className={`my-auto h-1.5 w-full ${SKELETON}`} />
      )}
    </span>
  );
}

function Row({ item, onNavigate }: { item: WatchlistItem; onNavigate: () => void }) {
  const money = useMoney();
  const { asset } = item;
  return (
    <li>
      <Link href={`/monedas/${asset.slug}`} onClick={onNavigate} className={`grid cursor-pointer grid-cols-[minmax(0,1fr)_auto_7.5rem] items-center gap-3 rounded-xl px-3 py-2.5 ${ROW_HOVER}`}>
        <span className="flex min-w-0 items-center gap-3">
          <CoinIcon url={asset.imageUrl} size={36} />
          <span className="min-w-0">
            <span className="block truncate font-bold">{asset.symbol.toUpperCase()}</span>
            <span className="block truncate text-sm text-soft">{formatCompactMoney(asset.marketCapUsd, money.currency, money.rate)}</span>
          </span>
        </span>
        <span className="text-right font-bold tabular-nums">{money.price(asset.priceUsd)}</span>
        <span className="flex flex-col items-end gap-0.5 text-sm font-semibold">
          <Spark slug={asset.slug} />
          <PctText value={asset.change24hPct} />
        </span>
      </Link>
    </li>
  );
}

function Panel({ onClose }: { onClose: () => void }) {
  const [items, setItems] = useState<WatchlistItem[] | null>(null);

  useEffect(() => {
    let stopped = false;
    const controller = new AbortController();
    const load = async () => {
      try {
        const response = await fetch("/api/watchlist", { cache: "no-store", signal: controller.signal });
        if (response.status === 401) {
          redirectToLogin();
          return;
        }
        if (!response.ok) return;
        const next = (await response.json()) as WatchlistItem[];
        if (!stopped) setItems(next);
      } catch {
        /* se reintenta en la siguiente vuelta */
      }
    };
    void load();
    const timer = setInterval(() => void load(), REFRESH_MS);
    return () => {
      stopped = true;
      controller.abort();
      clearInterval(timer);
    };
  }, []);

  return (
    <>
      <div className="flex items-center justify-between gap-4 px-5 pb-3 pt-5">
        <h2 className="text-lg font-bold">Mi lista de seguimiento</h2>
        <Link href="/seguimiento" onClick={onClose} className="inline-flex items-center gap-1 font-semibold text-accent transition-opacity hover:opacity-80">
          Ver todo <ChevronRightIcon className="size-4" />
        </Link>
      </div>
      <div className="grid grid-cols-[minmax(0,1fr)_auto_7.5rem] gap-3 border-y border-rule px-6 py-2 text-sm text-soft">
        <span>Capitalización</span>
        <span className="text-right">Precio</span>
        <span className="text-right">24h %</span>
      </div>
      <ul aria-busy={items === null} className="scrollbar-none max-h-[min(30rem,calc(100dvh-12rem))] overflow-y-auto p-2">
        {items === null &&
          Array.from({ length: 6 }, (_, i) => (
            <li key={i} aria-hidden="true" className="flex items-center gap-3 px-3 py-3">
              <span className={`size-9 rounded-full ${SKELETON}`} />
              <span className={`h-4 flex-1 blur-[2px] ${SKELETON}`} />
            </li>
          ))}
        {items?.length === 0 && <li className="px-3 py-6 text-center text-soft">Todavía no sigues ninguna moneda.</li>}
        {items?.map((item) => <Row key={item.asset.id} item={item} onNavigate={onClose} />)}
      </ul>
    </>
  );
}

/**
 * «Seguimiento» de la barra superior. Pulsarlo lleva a la lista (/seguimiento); solo al pasar el ratón por encima (o
 * al enfocarlo con el teclado) se abre el panel con tus monedas. «Ver todo» lleva también a la lista completa.
 */
export function WatchlistMenu({ signedIn }: { signedIn: boolean }) {
  const [anchor, setAnchor] = useState<HTMLAnchorElement | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const close = useCallback(() => {
    clearTimeout(timer.current);
    setAnchor(null);
  }, []);
  const keep = useCallback(() => clearTimeout(timer.current), []);
  const leave = useCallback(() => {
    clearTimeout(timer.current);
    timer.current = setTimeout(() => setAnchor(null), 180);
  }, []);
  useEffect(() => () => clearTimeout(timer.current), []);

  if (!signedIn) {
    return (
      <Link href="/seguimiento" className={NAV_LINK}>
        <StarIcon className="size-4" /> Seguimiento
      </Link>
    );
  }

  return (
    <>
      <Link
        href="/seguimiento"
        onPointerEnter={(event) => {
          if (event.pointerType === "touch") return;
          keep();
          setAnchor(event.currentTarget);
        }}
        onPointerLeave={leave}
        onClick={close}
        className={`${NAV_LINK} ${anchor ? "bg-ink/10" : ""}`}
      >
        <StarIcon className="size-4" /> Seguimiento
      </Link>
      {anchor && (
        <PopPanel
          anchor={anchor}
          label="Mi lista de seguimiento"
          role="dialog"
          autoFocus={false}
          onClose={close}
          onPointerEnter={keep}
          onPointerLeave={leave}
          className="w-[27rem] max-w-[calc(100vw-1rem)] rounded-2xl"
        >
          <Panel onClose={close} />
        </PopPanel>
      )}
    </>
  );
}
