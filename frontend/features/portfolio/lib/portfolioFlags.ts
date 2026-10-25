"use client";

import { useEffect, useRef, useSyncExternalStore } from "react";
import type { PortfolioInfo } from "../../../shared/lib/types";

// Cambios que el usuario acaba de hacer en un portafolio («Predeterminado», «Contabilizar»). Se ven al instante y se
// descartan cuando el servidor, ya recargado, trae el mismo valor (o si el guardado falla).

type Flags = { isDefault?: boolean; includeInTotal?: boolean };

const EMPTY: Record<string, Flags> = {};
let overrides = EMPTY;
const listeners = new Set<() => void>();

function emit(next: Record<string, Flags>) {
  overrides = next;
  listeners.forEach((listener) => listener());
}

// «Predeterminado» es único: al marcar uno, el resto se ve apagado al instante (undefined = sin cambio pendiente).
let exclusiveDefault: string | null | undefined;

export function setExclusiveDefault(id: string | null | undefined): void {
  exclusiveDefault = id;
  listeners.forEach((listener) => listener());
}

let saving = false;

/** Una sola escritura de ajustes a la vez en toda la pantalla; si hay una en curso, la nueva se ignora. */
export async function runExclusive(task: () => Promise<void>): Promise<void> {
  if (saving) return;
  saving = true;
  try {
    await task();
  } finally {
    saving = false;
  }
}

export function patchFlags(id: string, patch: Flags): void {
  emit({ ...overrides, [id]: { ...overrides[id], ...patch } });
}

export function clearFlags(id: string): void {
  if (!(id in overrides)) return;
  const { [id]: _removed, ...rest } = overrides;
  emit(rest);
}

const getExclusive = () => exclusiveDefault;

const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};

/** Los valores que se deben mostrar: lo recién cambiado o, si no hay nada pendiente, lo que dice el servidor. */
export function useFlags(portfolio: Pick<PortfolioInfo, "id" | "isDefault" | "includeInTotal">): { isDefault: boolean; includeInTotal: boolean } {
  const all = useSyncExternalStore(subscribe, () => overrides, () => EMPTY);
  const exclusive = useSyncExternalStore(subscribe, getExclusive, () => undefined);
  const seen = useRef({ isDefault: portfolio.isDefault, includeInTotal: portfolio.includeInTotal });

  useEffect(() => {
    if (seen.current.isDefault === portfolio.isDefault && seen.current.includeInTotal === portfolio.includeInTotal) return;
    const defaultChanged = seen.current.isDefault !== portfolio.isDefault;
    seen.current = { isDefault: portfolio.isDefault, includeInTotal: portfolio.includeInTotal };
    clearFlags(portfolio.id);
    if (defaultChanged) setExclusiveDefault(undefined);
  }, [portfolio.id, portfolio.isDefault, portfolio.includeInTotal]);

  const own = all[portfolio.id];
  const isDefault = exclusive === undefined ? (own?.isDefault ?? portfolio.isDefault) : exclusive === portfolio.id;
  return { isDefault, includeInTotal: own?.includeInTotal ?? portfolio.includeInTotal };
}

let refreshTimer: ReturnType<typeof setTimeout> | undefined;

/** Recarga los datos del servidor una sola vez aunque se pidan varias seguidas (cada recarga cuesta varias peticiones). */
export function refreshSoon(router: { refresh: () => void }, ms = 400): void {
  clearTimeout(refreshTimer);
  refreshTimer = setTimeout(() => router.refresh(), ms);
}
