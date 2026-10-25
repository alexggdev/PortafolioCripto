import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { LoadError } from "@/shared/components/LoadError";
import { AlertsView } from "@/features/alerts/components/AlertsView";
import type { PickedCoin } from "@/features/portfolio/components/CoinPicker";
import { apiGet } from "@/shared/lib/api";
import { apiRequest } from "@/shared/lib/apiRequest";
import { requireSession } from "@/features/auth/lib/session";
import { isValidSlug } from "@/features/market/lib/slug";
import type { AssetSummary, PriceAlert } from "@/shared/lib/types";

export const metadata: Metadata = { title: "Alertas de precio", robots: { index: false } };
export const dynamic = "force-dynamic";

type Props = { searchParams: Promise<{ moneda?: string | string[] }> };

export default async function AlertsPage({ searchParams }: Props) {
  const { moneda } = await searchParams;
  const slug = typeof moneda === "string" && isValidSlug(moneda) ? moneda : null;

  const here = slug ? `/alertas?moneda=${encodeURIComponent(slug)}` : "/alertas";
  const session = await requireSession(here);

  const result = await apiRequest<PriceAlert[]>("GET", "/api/alerts", { token: session.token });
  if (result.status === 401) redirect(`/api/session/expired?next=${encodeURIComponent(here)}`);
  const failed = !result.ok || !result.data;

  // Si se llega desde la ficha de una moneda, el formulario ya la trae elegida. Si no existe, se ignora.
  let initialCoin: PickedCoin | null = null;
  if (slug && !failed) {
    try {
      const asset = await apiGet<AssetSummary>(`/api/assets/${encodeURIComponent(slug)}`);
      initialCoin = { slug: asset.slug, symbol: asset.symbol, name: asset.name, imageUrl: asset.imageUrl };
    } catch {
      initialCoin = null;
    }
  }

  return (
    <>
      <h1 className="condensed mt-6 text-4xl font-bold leading-none tracking-tight sm:text-5xl">Alertas de precio</h1>
      <p className="mt-3 max-w-prose text-soft">Te avisamos cuando una moneda llegue al precio que elijas.</p>
      {result.ok && result.data ? (
        <AlertsView initial={result.data} initialCoin={initialCoin} />
      ) : (
        <LoadError message={result.errors.join(" ")} />
      )}
    </>
  );
}
