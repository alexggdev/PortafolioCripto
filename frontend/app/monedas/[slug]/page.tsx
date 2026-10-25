import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { cache } from "react";
import { CoinLive } from "@/features/market/components/CoinLive";
import { PriceChart } from "@/features/market/components/PriceChart";
import { ApiError, apiGet } from "@/shared/lib/api";
import { DEFAULT_RANGE } from "@/features/market/lib/ranges";
import { getSession } from "@/features/auth/lib/session";
import { isValidSlug } from "@/features/market/lib/slug";
import { loadWatchedSlugs } from "@/features/watchlist/lib/watchlistServer";
import type { AssetChart, AssetSummary } from "@/shared/lib/types";

export const dynamic = "force-dynamic";

const getAsset = cache((slug: string) => apiGet<AssetSummary>(`/api/assets/${encodeURIComponent(slug)}`));
const getChart = cache((slug: string) =>
  apiGet<AssetChart>(`/api/assets/${encodeURIComponent(slug)}/chart?range=${DEFAULT_RANGE}`),
);

type Props = { params: Promise<{ slug: string }> };

async function loadOrNotFound<T>(promise: Promise<T>): Promise<T> {
  try {
    return await promise;
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  if (!isValidSlug(slug)) return { title: "Moneda no encontrada" };
  try {
    const asset = await getAsset(slug);
    return { title: `${asset.name} (${asset.symbol})` };
  } catch {
    return { title: "Moneda" };
  }
}

export default async function CoinPage({ params }: Props) {
  const { slug } = await params;
  if (!isValidSlug(slug)) notFound();

  const session = await getSession();
  const [asset, chart, watched] = await Promise.all([
    loadOrNotFound(getAsset(slug)),
    loadOrNotFound(getChart(slug)),
    loadWatchedSlugs(session),
  ]);

  return (
    <>
      <Link href="/" className="mt-6 inline-block text-sm font-medium text-accent underline underline-offset-4">
        ← Volver al mercado
      </Link>
      <div className="mt-5">
        <CoinLive initial={asset} watched={watched.includes(asset.slug)} signedIn={session !== null} />
        <PriceChart slug={asset.slug} initial={chart} />
      </div>
    </>
  );
}
