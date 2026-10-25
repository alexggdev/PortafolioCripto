// Forma de las respuestas de la API de .NET (JSON en camelCase).

export type PriceSource = "live" | "delayed";

export interface AssetSummary {
  id: number;
  slug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
  rank: number | null;
  priceUsd: number | null;
  priceSource: PriceSource | null;
  priceUpdatedAtUtc: string | null;
  marketCapUsd: number | null;
  volume24hUsd: number | null;
  change1hPct: number | null;
  change24hPct: number | null;
  change7dPct: number | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type SortKey = "rank" | "name" | "price" | "change24h" | "marketCap" | "volume";

export type RangeCode = "7D" | "1M" | "3M" | "1Y" | "All";

export interface ChartPoint {
  /** Día UTC en formato AAAA-MM-DD. */
  date: string;
  priceUsd: number;
}

export interface AssetChart {
  slug: string;
  range: RangeCode;
  points: ChartPoint[];
  changePct: number | null;
  includesCurrentPrice: boolean;
}

// ── Portafolio (todo llega en USD) ──────────────────────────────────────────────────────────

export type TransactionType = "Buy" | "Sell" | "TransferIn" | "TransferOut" | "Staking" | "Airdrop" | "Fee";

export interface PositionDto {
  assetId: number;
  slug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
  quantity: number;
  averageCostUsd: number;
  costBasisUsd: number;
  priceUsd: number | null;
  priceSource: PriceSource | null;
  priceUpdatedAtUtc: string | null;
  valueUsd: number | null;
  unrealizedPnlUsd: number | null;
  unrealizedPnlPct: number | null;
  realizedPnlUsd: number;
  allocationPct: number | null;
  change24hPct: number | null;
  change1hPct?: number | null;
  change7dPct?: number | null;
}

export interface PortfolioSummary {
  portfolioId: string | null;
  name: string;
  valueUsd: number;
  costBasisUsd: number;
  unrealizedPnlUsd: number;
  unrealizedPnlPct: number | null;
  realizedPnlUsd: number;
  totalPnlUsd: number;
  change24hUsd: number;
  change24hPct: number | null;
  unpricedPositions: number;
  inconsistentAssets: number;
  costBasisMethod: string;
  positions: PositionDto[];
}

export interface TransactionDto {
  id: number;
  assetSlug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
  type: TransactionType;
  quantity: number;
  unitPriceUsd: number;
  feeUsd: number;
  dateUtc: string;
  note: string | null;
}

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
}

export interface PortfolioHistoryPoint {
  /** Día UTC en formato AAAA-MM-DD. */
  date: string;
  valueUsd: number;
  netContributedUsd: number;
}

export interface PortfolioHistory {
  portfolioId: string | null;
  range: RangeCode;
  firstTransactionDate: string | null;
  points: PortfolioHistoryPoint[];
  periodProfitUsd: number | null;
  includesCurrentValue: boolean;
}

export interface PortfolioValue {
  id: string;
  name: string;
  positionCount: number;
  valueUsd: number;
  costBasisUsd: number;
  unrealizedPnlUsd: number;
  unrealizedPnlPct: number | null;
  realizedPnlUsd: number;
  change24hUsd: number;
  change24hPct: number | null;
  /** Falso: se ve en el menú, pero no suma en el total, las monedas ni la asignación. */
  includeInTotal: boolean;
}

/** Cuánto tiene un portafolio de una moneda. */
export interface HolderShare {
  portfolioId: string;
  quantity: number;
}

/** Todos los portafolios del usuario sumados (vista «Resumen»). */
export interface PortfolioOverview {
  valueUsd: number;
  costBasisUsd: number;
  unrealizedPnlUsd: number;
  unrealizedPnlPct: number | null;
  realizedPnlUsd: number;
  change24hUsd: number;
  change24hPct: number | null;
  unpricedPositions: number;
  portfolios: PortfolioValue[];
  positions: PositionDto[];
  /** Slug de cada moneda → portafolios que la tienen y cuánto. */
  holders: Record<string, HolderShare[]>;
}

export interface PortfolioInfo {
  id: string;
  name: string;
  createdAtUtc: string;
  transactionCount: number;
  /** Imagen elegida (0 a 27); null si no eligió. */
  avatar: number | null;
  includeInTotal: boolean;
  isDefault: boolean;
}

// ── Lista de seguimiento ────────────────────────────────────────────────────────────────────

export interface WatchlistItem {
  asset: AssetSummary;
  addedAtUtc: string;
}

// ── Alertas de precio ───────────────────────────────────────────────────────────────────────

export type AlertDirection = "Above" | "Below";

export interface PriceAlert {
  id: number;
  assetSlug: string;
  symbol: string;
  name: string;
  imageUrl: string | null;
  direction: AlertDirection;
  targetPriceUsd: number;
  /** Solo en alertas activas y si hay un precio reciente. */
  currentPriceUsd: number | null;
  createdAtUtc: string;
  triggeredAtUtc: string | null;
  triggeredPriceUsd: number | null;
  /** Disparada y todavía sin ver. */
  unread: boolean;
}
