// Pequeñas reglas sobre la lista de portafolios (sin React, para probarlas).

export const MAX_NAME = 100;

/** El portafolio que se abre al entrar: el marcado como predeterminado o, si no hay, el primero. */
export function pickDefault<T extends { isDefault: boolean }>(list: readonly T[]): T | null {
  return list.find((p) => p.isDefault) ?? list[0] ?? null;
}

/** Mueve el elemento de la posición `from` a la posición `to` (las demás se desplazan). */
export function moveItem<T>(items: readonly T[], from: number, to: number): T[] {
  if (from === to || from < 0 || to < 0 || from >= items.length || to >= items.length) return [...items];
  const next = [...items];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

/** Posición final al arrastrar una fila `from` una distancia `dy` (px) cuando cada fila ocupa `step` px. */
export function dropIndex(from: number, dy: number, step: number, count: number): number {
  if (step <= 0 || count <= 0) return from;
  return Math.min(Math.max(from + Math.round(dy / step), 0), count - 1);
}

/** Nombre propuesto al duplicar: «Largo plazo Copia», sin pasar del máximo. */
export function copyName(name: string): string {
  const suffix = " Copia";
  return name.slice(0, MAX_NAME - suffix.length).trimEnd() + suffix;
}

/** Suma solo los portafolios que cuentan en el total. */
export function totalOfIncluded(values: readonly { valueUsd: number; includeInTotal: boolean }[]): number {
  return values.reduce((sum, v) => (v.includeInTotal ? sum + v.valueUsd : sum), 0);
}
