import type { Metadata } from "next";
import { CurrencyNote } from "@/features/currency/components/CurrencyNote";
import { MarketTable } from "@/features/market/components/MarketTable";
import { Pagination } from "@/features/market/components/Pagination";
import { SearchBox } from "@/features/market/components/SearchBox";
import { apiGet } from "@/shared/lib/api";
import { getSession } from "@/features/auth/lib/session";
import { loadWatchedSlugs } from "@/features/watchlist/lib/watchlistServer";
import { parseListQuery, toApiQuery, toSearchParams } from "@/features/market/lib/query";
import type { AssetSummary, PagedResult } from "@/shared/lib/types";

export const metadata: Metadata = {
  title: "Mercado",
};

export const dynamic = "force-dynamic";

type Props = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function MarketPage({ searchParams }: Props) {
  const query = parseListQuery(await searchParams);
  const session = await getSession();
  const [data, watched] = await Promise.all([
    apiGet<PagedResult<AssetSummary>>(`/api/assets?${toApiQuery(query)}`),
    loadWatchedSlugs(session),
  ]);

  return (
    <>
      <h1 className="condensed mt-6 text-4xl font-bold leading-none tracking-tight sm:text-5xl">Mercado</h1>
      <p className="mt-3 max-w-prose text-soft">
        {query.search
          ? `${data.totalCount.toLocaleString("es-ES")} ${data.totalCount === 1 ? "resultado" : "resultados"} para «${query.search}».`
          : `Precios de las ${data.totalCount.toLocaleString("es-ES")} criptomonedas más grandes por capitalización.`}
      </p>
      <CurrencyNote />

      <div className="mt-8 mb-5">
        <SearchBox query={query} />
      </div>

      <MarketTable key={toSearchParams(query).toString()} initial={data} query={query} watched={watched} signedIn={session !== null} />
      <Pagination query={query} totalPages={data.totalPages} />
    </>
  );
}
