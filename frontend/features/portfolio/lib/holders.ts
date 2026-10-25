import type { HolderShare } from "../../../shared/lib/types";

export interface HolderLine {
  portfolioId: string;
  quantity: number;
  /** Cantidad × precio; null si la moneda no tiene precio. */
  valueUsd: number | null;
}

/** Lo que tiene cada portafolio de una moneda, con su valor al precio actual. */
export function holderLines(shares: readonly HolderShare[] | undefined, priceUsd: number | null): HolderLine[] {
  return (shares ?? []).map((s) => ({ portfolioId: s.portfolioId, quantity: s.quantity, valueUsd: priceUsd === null ? null : s.quantity * priceUsd }));
}
