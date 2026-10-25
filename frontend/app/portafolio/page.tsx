import type { Metadata } from "next";
import { cookies } from "next/headers";
import { OverviewScreen } from "./_screens/OverviewScreen";
import { CoinScreen } from "./_screens/CoinScreen";
import { PortfolioScreen } from "./_screens/PortfolioScreen";
import { parseScreen, SELECTED_COOKIE, selectedId, VIEW_COOKIE } from "@/features/portfolio/lib/selectedPortfolio";

export const metadata: Metadata = { title: "Mi portafolio", robots: { index: false } };
export const dynamic = "force-dynamic";

/**
 * Toda la sección vive en /portafolio: la vista general, un portafolio y una moneda son pantallas que se cambian
 * por dentro (la elegida se guarda en una cookie), así que la dirección nunca cambia.
 */
export default async function PortfolioPage() {
  const store = await cookies();
  const requested = selectedId(store.get(SELECTED_COOKIE)?.value);
  const screen = parseScreen(store.get(VIEW_COOKIE)?.value);

  if (screen.kind === "resumen") return <OverviewScreen />;
  if (screen.kind === "moneda") return <CoinScreen key={screen.slug} slug={screen.slug} requested={requested} />;
  return <PortfolioScreen requested={requested} />;
}
