import { redirect } from "next/navigation";
import { OverviewView } from "@/features/portfolio/components/OverviewView";
import { apiRequest } from "@/shared/lib/apiRequest";
import { requireSession } from "@/features/auth/lib/session";
import type { PortfolioInfo, PortfolioOverview } from "@/shared/lib/types";

export async function OverviewScreen() {
  const session = await requireSession("/portafolio");
  const [result, list] = await Promise.all([
    apiRequest<PortfolioOverview>("GET", "/api/portfolio/overview", { token: session.token }),
    apiRequest<PortfolioInfo[]>("GET", "/api/portfolios", { token: session.token }),
  ]);
  if (result.status === 401 || list.status === 401) redirect("/api/session/expired?next=%2Fportafolio");
  if (!result.ok || !result.data) throw new Error(result.errors.join(" "));

  if (!list.ok || !list.data) throw new Error(list.errors.join(" "));

  return <OverviewView initial={result.data} portfolios={list.data} />;
}
