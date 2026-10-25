import { redirect } from "next/navigation";
import { OverviewScreen } from "./OverviewScreen";
import { PortfolioView } from "@/features/portfolio/components/PortfolioView";
import { apiRequest } from "@/shared/lib/apiRequest";
import { pickPortfolio } from "@/features/portfolio/lib/selectedPortfolio";
import { requireSession } from "@/features/auth/lib/session";
import type { PortfolioHistory, PortfolioInfo, PortfolioOverview, PortfolioSummary } from "@/shared/lib/types";

export async function PortfolioScreen({ requested }: { requested: string | null }) {
  const session = await requireSession("/portafolio");
  const expired = `/api/session/expired?next=${encodeURIComponent("/portafolio")}`;

  // La API puede tardar o rechazar peticiones un momento (límite por minuto, recarga): se reintenta antes de rendirse.
  const fetchList = () => apiRequest<PortfolioInfo[]>("GET", "/api/portfolios", { token: session.token });
  let list = await fetchList();
  for (const wait of [1200, 3000]) {
    if (list.ok || list.status === 401) break;
    await new Promise((resolve) => setTimeout(resolve, wait));
    list = await fetchList();
  }
  if (list.status === 401) redirect(expired);
  if (!list.ok || !list.data) throw new Error(list.errors.join(" "));

  // La selección guardada solo vale si es de este usuario; si no, se abre el predeterminado y, si no hay, la vista general.
  const portfolios = list.data;
  const selected = pickPortfolio(portfolios, requested);
  if (!selected) return <OverviewScreen />; // sin predeterminado ni elegido: la vista general
  const id = selected.id;
  const suffix = id ? `portfolioId=${id}` : "";

  const loadAll = () =>
    Promise.all([
      apiRequest<PortfolioSummary>("GET", `/api/portfolio?${suffix}`, { token: session.token }),
      apiRequest<PortfolioHistory>("GET", `/api/portfolio/history?range=1M&${suffix}`, { token: session.token }),
      apiRequest<PortfolioOverview>("GET", "/api/portfolio/overview", { token: session.token }),
    ]);
  let [result, history, overview] = await loadAll();
  for (const wait of [1200, 3000]) {
    if (result.ok || result.status === 401) break;
    await new Promise((resolve) => setTimeout(resolve, wait));
    [result, history, overview] = await loadAll();
  }

  // La API no acepta el token (p. ej. cambió la clave): se limpia la sesión y se vuelve a entrar.
  if (result.status === 401 || history.status === 401) redirect(expired);
  if (!result.ok || !result.data) throw new Error(result.errors.join(" "));

  // La gráfica es secundaria: si falla, la página sigue y la gráfica lo reintenta sola desde el navegador.
  return (
    <PortfolioView
      key={id}
      initial={result.data}
      initialHistory={history.ok ? history.data : null}
      portfolios={portfolios}
      portfolioId={id}
      values={overview.ok ? (overview.data?.portfolios ?? null) : null}
    />
  );
}
