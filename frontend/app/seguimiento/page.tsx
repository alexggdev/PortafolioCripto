import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { LoadError } from "@/shared/components/LoadError";
import { WatchlistView } from "@/features/watchlist/components/WatchlistView";
import { apiRequest } from "@/shared/lib/apiRequest";
import { requireSession } from "@/features/auth/lib/session";
import type { WatchlistItem } from "@/shared/lib/types";

export const metadata: Metadata = { title: "Seguimiento", robots: { index: false } };
export const dynamic = "force-dynamic";

export default async function WatchlistPage() {
  const session = await requireSession("/seguimiento");
  const result = await apiRequest<WatchlistItem[]>("GET", "/api/watchlist", { token: session.token });

  if (result.status === 401) redirect("/api/session/expired?next=%2Fseguimiento");

  return (
    <>
      <h1 className="condensed mt-6 text-4xl font-bold leading-none tracking-tight sm:text-5xl">Seguimiento</h1>
      <p className="mt-3 max-w-prose text-soft">Las monedas que marcas con la estrella, con su precio en vivo.</p>
      {result.ok && result.data ? <WatchlistView initial={result.data} /> : <LoadError message={result.errors.join(" ")} />}
    </>
  );
}
