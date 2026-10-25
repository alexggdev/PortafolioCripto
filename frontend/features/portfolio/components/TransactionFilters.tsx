"use client";

import { Dropdown, type DropdownOption } from "@/shared/components/Dropdown";
import { GridIcon } from "@/shared/components/Icons";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { TypeBadge } from "@/features/portfolio/components/TypeBadge";
import { TYPE_OPTIONS } from "@/features/portfolio/lib/transactions";
import type { TransactionType } from "@/shared/lib/types";

export interface TransactionFilterState {
  type: TransactionType | "all";
  /** Slug de la moneda o «all». */
  coin: string;
}

export const NO_FILTERS: TransactionFilterState = { type: "all", coin: "all" };

export interface FilterCoin {
  slug: string;
  symbol: string;
  imageUrl: string | null;
}

const ALL_ICON = <GridIcon className="size-5 shrink-0 text-soft" />;

/** Los dos filtros de la pestaña «Transacciones»: por tipo y por moneda. Las listas se abren hacia arriba. */
export function TransactionFilters({
  value,
  onChange,
  coins,
}: {
  value: TransactionFilterState;
  onChange: (next: TransactionFilterState) => void;
  coins: readonly FilterCoin[];
}) {
  const typeOptions: DropdownOption<TransactionFilterState["type"]>[] = [
    { value: "all", label: "Todos los tipos", icon: ALL_ICON },
    ...TYPE_OPTIONS.map((o) => ({ value: o.value, label: o.label, icon: <TypeBadge type={o.value} size={26} /> })),
  ];
  const coinOptions: DropdownOption<string>[] = [
    { value: "all", label: "Todas las monedas", icon: ALL_ICON },
    ...coins.map((c) => ({ value: c.slug, label: c.symbol.toUpperCase(), icon: <CoinIcon url={c.imageUrl} size={26} /> })),
  ];

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Dropdown label="Filtrar por tipo" value={value.type} options={typeOptions} onChange={(type) => onChange({ ...value, type })} />
      {coins.length > 0 && (
        <Dropdown label="Filtrar por moneda" value={value.coin} options={coinOptions} onChange={(coin) => onChange({ ...value, coin })} searchable />
      )}
    </div>
  );
}
