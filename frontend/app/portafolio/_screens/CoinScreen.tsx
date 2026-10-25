import { notFound, redirect } from "next/navigation";
import { CoinPositionView, type CoinInfo } from "@/features/portfolio/components/CoinPositionView";
import { apiRequest } from "@/shared/lib/apiRequest";
import { pickPortfolio } from "@/features/portfolio/lib/selectedPortfolio";
import { requireSession } from "@/features/auth/lib/session";
import type { AssetSummary, PortfolioInfo, PortfolioOverview, PortfolioSummary } from "@/shared/lib/types";

/** Una moneda dentro del portafolio abierto: sus transacciones, cantidad y ganancia. */
export async function CoinScreen({ slug, requested }: { slug: string; requested: string | null }) {
  const session = await requireSession("/portafolio");
  const expired = `/api/session/expired?next=${encodeURIComponent("/portafolio")}`;

  const list = await apiRequest<PortfolioInfo[]>("GET", "/api/portfolios", { token: session.token });
  if (list.status === 401) redirect(expired);
  if (!list.ok || !list.data) throw new Error(list.errors.join(" "));

  const portfolios = list.data;
  const selected = pickPortfolio(portfolios, requested) ?? portfolios[0] ?? null;
  if (!selected) throw new Error("No hay portafolios.");

  const [summary, overview, asset] = await Promise.all([
    apiRequest<PortfolioSummary>("GET", `/api/portfolio?portfolioId=${selected.id}`, { token: session.token }),
    apiRequest<PortfolioOverview>("GET", "/api/portfolio/overview", { token: session.token }),
    apiRequest<AssetSummary>("GET", `/api/assets/${encodeURIComponent(slug)}`),
  ]);
  if (summary.status === 401) redirect(expired);
  if (!summary.ok || !summary.data) throw new Error(summary.errors.join(" "));
  if (asset.status === 404) notFound();

  const position = summary.data.positions.find((x) => x.slug === slug) ?? null;
  const coin: CoinInfo | null = position
    ? { slug: position.slug, symbol: position.symbol, name: position.name, imageUrl: position.imageUrl }
    : asset.ok && asset.data
      ? { slug: asset.data.slug, symbol: asset.data.symbol, name: asset.data.name, imageUrl: asset.data.imageUrl }
      : null;
  if (!coin) throw new Error("No se pudo cargar la moneda.");

  return (
    <CoinPositionView
      key={`${selected.id}:${slug}`}
      coin={coin}
      initial={summary.data}
      portfolios={portfolios}
      portfolioId={selected.id}
      values={overview.ok ? (overview.data?.portfolios ?? null) : null}
    />
  );
}
