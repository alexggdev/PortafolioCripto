"use client";

import { useRouter } from "next/navigation";
import { CopyIcon, PencilIcon, TrashIcon } from "@/shared/components/Icons";
import { PopMenu, type PopMenuItem } from "@/shared/components/PopMenu";
import { clearFlags, patchFlags, refreshSoon, runExclusive, setExclusiveDefault, useFlags } from "@/features/portfolio/lib/portfolioFlags";
import { updatePortfolio } from "@/features/portfolio/lib/portfolioApi";
import type { PortfolioInfo } from "@/shared/lib/types";

export type PortfolioAction = "edit" | "duplicate" | "delete";
type Key = PortfolioAction | "include" | "default";

const ICON = "size-4";

/**
 * Menú «···» de un portafolio: Editar, Duplicar y Eliminar. Con `withSettings` añade los dos interruptores
 * («Contabilizar» y «Predeterminada»), que se guardan al pulsarlos.
 */
export function PortfolioMenu({
  portfolio,
  onAction,
  withSettings = false,
  buttonClassName,
  align,
}: {
  portfolio: PortfolioInfo;
  onAction: (action: PortfolioAction) => void;
  withSettings?: boolean;
  buttonClassName?: string;
  /** «start»: el menú se abre desde el botón hacia la derecha. */
  align?: "start" | "end";
}) {
  const router = useRouter();
  const flags = useFlags(portfolio);

  function toggle(key: "include" | "default") {
    const field = key === "include" ? "includeInTotal" : "isDefault";
    const next = !flags[field];
    return runExclusive(async () => {
      patchFlags(portfolio.id, { [field]: next }); // se ve al instante; si el servidor falla, vuelve atrás
      if (key === "default") setExclusiveDefault(next ? portfolio.id : null);
      const result = await updatePortfolio(portfolio.id, { [field]: next });
      if (result.ok) {
        refreshSoon(router);
      } else {
        clearFlags(portfolio.id);
        setExclusiveDefault(undefined);
      }
    });
  }

  const items: PopMenuItem<Key>[] = [
    { key: "edit", text: "Editar", icon: <PencilIcon className={ICON} /> },
    { key: "duplicate", text: "Duplicar", icon: <CopyIcon className={ICON} /> },
    { key: "delete", text: "Eliminar", icon: <TrashIcon className={ICON} /> },
  ];
  if (withSettings) {
    items.push(
      { key: "include", text: "Contabilizar en mi portafolio", description: "Los activos de este portafolio se incluirán en el valor total", checked: flags.includeInTotal, separated: true },
      { key: "default", text: "Establecer como portafolio predeterminado", description: "Al entrar en Mi portafolio, se abrirá este por defecto.", checked: flags.isDefault },
    );
  }

  return (
    <PopMenu<Key>
      label={`Más acciones de ${portfolio.name}`}
      items={items}
      onChoose={(key) => (key === "include" || key === "default" ? void toggle(key) : onAction(key))}
      widthClass={withSettings ? "w-80" : "w-max min-w-36"}
      align={align}
      buttonClassName={buttonClassName}
    />
  );
}
