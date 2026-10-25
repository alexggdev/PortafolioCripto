"use client";

import { ListIcon, MoveIcon, TrashIcon } from "@/shared/components/Icons";
import { PopMenu, type PopMenuItem } from "@/shared/components/PopMenu";

export type AssetMenuChoice = "transactions" | "move" | "remove";

const ITEMS: readonly PopMenuItem<AssetMenuChoice>[] = [
  { key: "transactions", text: "Ver transacciones", icon: <ListIcon className="size-4" /> },
  { key: "move", text: "Mover a otro portafolio", icon: <MoveIcon className="size-4" /> },
  { key: "remove", text: "Quitar del portafolio", danger: true, icon: <TrashIcon className="size-4" /> },
];

/** Menú «···» de una moneda en la tabla de activos. */
export function AssetMenu({ label, onChoose }: { label: string; onChoose: (choice: AssetMenuChoice) => void }) {
  return <PopMenu label={`Más acciones de ${label}`} items={ITEMS} onChoose={onChoose} />;
}
