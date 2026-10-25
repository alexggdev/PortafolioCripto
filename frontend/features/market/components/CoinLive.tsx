"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { CurrencyNote } from "@/features/currency/components/CurrencyNote";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { PctText } from "@/shared/components/PctText";
import { UpdatedAgo } from "@/features/market/components/UpdatedAgo";
import { WatchStar } from "@/features/watchlist/components/WatchStar";
import { NO_VALUE } from "@/shared/lib/format";
import { usePolledJson } from "@/shared/lib/usePolledJson";
import type { AssetSummary } from "@/shared/lib/types";

const POLL_MS = 10_000;

/** Cabecera de la ficha: nombre, precio grande que se refresca solo y cifras clave. */
export function CoinLive({
  initial,
  watched = false,
  signedIn = false,
}: {
  initial: AssetSummary;
  watched?: boolean;
  signedIn?: boolean;
}) {
  const money = useMoney();
  const { data: asset, updatedAt, failing } = usePolledJson<AssetSummary>(
    `/api/assets/${encodeURIComponent(initial.slug)}`,
    initial,
    POLL_MS,
  );

  const [flash, setFlash] = useState<{ dir: "up" | "down"; n: number } | null>(null);
  const last = useRef(initial.priceUsd);
  useEffect(() => {
    const previous = last.current;
    if (previous != null && asset.priceUsd != null && previous !== asset.priceUsd) {
      setFlash({ dir: asset.priceUsd > previous ? "up" : "down", n: Date.now() });
    }
    last.current = asset.priceUsd;
  }, [asset.priceUsd]);

  const delayed = asset.priceSource === "delayed";
  const heroPrice = money.price(asset.priceUsd);

  return (
    <section aria-label={`Precio de ${asset.name}`}>
      <div className="flex items-center gap-3">
        {asset.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={asset.imageUrl}
            alt=""
            width={40}
            height={40}
            referrerPolicy="no-referrer"
            className="size-9 rounded-full sm:size-10"
          />
        ) : (
          <span aria-hidden="true" className="inline-block size-9 rounded-full bg-rule sm:size-10" />
        )}
        <h1 className="condensed text-3xl font-bold leading-none tracking-tight sm:text-4xl">{asset.name}</h1>
        <span className="text-soft">{asset.symbol}</span>
        <WatchStar slug={asset.slug} name={asset.name} initialWatched={watched} signedIn={signedIn} />
        <Link href={`/alertas?moneda=${encodeURIComponent(asset.slug)}`} className="text-sm text-soft underline underline-offset-4 hover:text-ink">
          Crear alerta
        </Link>
        {asset.rank != null && (
          <span className="border border-rule px-2 py-0.5 text-sm text-soft">Puesto n.º {asset.rank}</span>
        )}
      </div>

      <p className="mt-6">
        <span
          key={flash ? `f-${flash.n}` : "f"}
          className={`hero-num inline-block px-1 font-semibold leading-none tracking-tight ${
            heroPrice.length > 12 ? "text-4xl sm:text-6xl" : "text-5xl sm:text-6xl"
          } ${
            flash ? (flash.dir === "up" ? "flash-up" : "flash-down") : ""
          }`}
        >
          {heroPrice}
        </span>
      </p>

      <p className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
        <span className="flex items-center gap-1.5 text-soft">
          <span
            aria-hidden="true"
            className={`inline-block size-2 rounded-full ${delayed ? "border border-ink" : "bg-ink"}`}
          />
          {delayed ? "Precio con retraso (CoinGecko, puede tener unos minutos)" : "Precio en vivo"}
        </span>
        <UpdatedAgo updatedAt={updatedAt} failing={failing} />
      </p>

      <dl className="mt-8 grid grid-cols-6 gap-px border border-rule bg-rule lg:grid-cols-12">
        <Stat wide label="Capitalización" value={money.whole(asset.marketCapUsd)} />
        <Stat wide label="Volumen 24 h" value={money.whole(asset.volume24hUsd)} />
        <Stat label={<><span className="max-sm:sr-only">Variación </span>1 h</>} value={<PctText value={asset.change1hPct} />} />
        <Stat label={<><span className="max-sm:sr-only">Variación </span>24 h</>} value={<PctText value={asset.change24hPct} />} />
        <Stat label={<><span className="max-sm:sr-only">Variación </span>7 d</>} value={<PctText value={asset.change7dPct} />} />
      </dl>
      <CurrencyNote />
    </section>
  );
}

function Stat({ label, value, wide = false }: { label: ReactNode; value: ReactNode; wide?: boolean }) {
  // Cuadrícula de 6 columnas (12 en pantallas grandes) para que nunca queden celdas vacías.
  return (
    <div className={`bg-surface px-4 py-3 ${wide ? "col-span-6 sm:col-span-3 lg:col-span-3" : "col-span-2"}`}>
      <dt className="text-sm text-soft">{label}</dt>
      <dd className="mt-1 text-base sm:text-lg font-medium tabular-nums">{value ?? NO_VALUE}</dd>
    </div>
  );
}
