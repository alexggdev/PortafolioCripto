import { apiRequest } from "../../../shared/lib/apiRequest";
import type { Session } from "../../auth/lib/session";
import type { WatchlistItem } from "../../../shared/lib/types";

/** Slugs que sigue el usuario. Sin sesión, o si la API falla, devuelve una lista vacía (la estrella se pinta apagada). */
export async function loadWatchedSlugs(session: Session | null): Promise<string[]> {
  if (!session) return [];
  const result = await apiRequest<WatchlistItem[]>("GET", "/api/watchlist", { token: session.token });
  if (!result.ok || !result.data) return [];
  return result.data.map((item) => item.asset.slug);
}
