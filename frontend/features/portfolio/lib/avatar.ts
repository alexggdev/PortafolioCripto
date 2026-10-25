// Las 28 imágenes pixeladas que se pueden elegir para un portafolio: 7 dibujos × 4 paletas. Sin React, para probarlas.

export const AVATAR_GRID = 12;
export const AVATAR_COUNT = 28;

const PALETTES: readonly (readonly string[])[] = [
  ["#f7d94c", "#f39c34", "#e0503f", "#9c2f2a"], // fuego
  ["#8be4f7", "#3f9de8", "#2458d8", "#132f72"], // mar
  ["#d3f7c0", "#59d68a", "#21a05c", "#0f5a3b"], // bosque
  ["#f6bcff", "#c86df2", "#7f40d8", "#3e2080"], // uva
];

const PATTERNS = 7;

function seedFrom(text: string): number {
  let h = 2166136261;
  for (const ch of text) {
    h ^= ch.charCodeAt(0);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

/** Ruido repetible (mulberry32 de un solo paso) para romper los degradados y dar el aspecto pixelado. */
function noise(seed: number): number {
  let t = (seed + 0x6d2b79f5) | 0;
  t = Math.imul(t ^ (t >>> 15), 1 | t);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

/** Posición 0..1 de la celda en la paleta, según el dibujo. */
function tone(pattern: number, x: number, y: number, seed: number): number {
  const c = (AVATAR_GRID - 1) / 2;
  const dx = x - c;
  const dy = y - c;
  const dist = Math.hypot(dx, dy) / Math.hypot(c, c);
  switch (pattern) {
    case 0:
      return (x + y) / (2 * (AVATAR_GRID - 1)); // diagonal
    case 1:
      return dist; // círculos
    case 2:
      return (dist * 2.4) % 1; // anillos
    case 3:
      return (y % 4) / 4 + 0.08; // franjas
    case 4:
      return ((x >> 1) + (y >> 1)) % 2 === 0 ? 0.2 : 0.7; // damero
    case 5:
      return noise(seed + Math.min(x, AVATAR_GRID - 1 - x) * 31 + y * 7); // mancha simétrica
    default:
      return (Math.atan2(dy, dx) / (2 * Math.PI) + 0.5) % 1; // rayos
  }
}

/** Los 144 colores (fila a fila) de la imagen número `index` (0 a 27). */
export function avatarCells(index: number): string[] {
  const i = ((Math.trunc(index) % AVATAR_COUNT) + AVATAR_COUNT) % AVATAR_COUNT;
  const palette = PALETTES[i % PALETTES.length];
  const pattern = Math.floor(i / PALETTES.length) % PATTERNS;
  const seed = i * 977;
  const cells: string[] = [];
  for (let y = 0; y < AVATAR_GRID; y++) {
    for (let x = 0; x < AVATAR_GRID; x++) {
      const jitter = (noise(seed + y * AVATAR_GRID + x) - 0.5) * (pattern === 5 ? 0 : 0.22);
      const t = Math.min(0.999, Math.max(0, tone(pattern, x, y, seed) + jitter));
      cells.push(palette[Math.floor(t * palette.length)]);
    }
  }
  return cells;
}

/** La imagen de un portafolio: la que eligió o, si no eligió, una según su id (siempre la misma). */
export function avatarIndex(portfolio: { id: string; avatar: number | null }): number {
  const chosen = portfolio.avatar;
  return chosen !== null && Number.isInteger(chosen) && chosen >= 0 && chosen < AVATAR_COUNT ? chosen : seedFrom(portfolio.id) % AVATAR_COUNT;
}
