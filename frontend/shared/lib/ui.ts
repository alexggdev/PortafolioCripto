/** Efecto de las filas de cualquier tabla al pasar el ratón: toda la fila se «enciende», sin cambiar colores de texto. */
export const ROW_HOVER = "transition-colors hover:bg-ink/5";

/** Botones pequeños de los formularios (fecha, comisión, notas): apagados en reposo y más claros al pasar el ratón. */
export const CHIP =
  "inline-flex cursor-pointer items-center gap-2 rounded-lg bg-rule px-3 py-2 text-sm font-medium text-soft transition-colors hover:bg-ink/15 hover:text-ink focus-visible:text-ink";

/** Bloque gris que parpadea mientras llegan los datos (nunca se muestra texto de «cargando»). */
export const SKELETON = "animate-pulse rounded-lg bg-rule";

/** Enlaces de la barra superior: al pasar el ratón se enciende un recuadro suave (sin subrayado). */
export const NAV_LINK =
  "inline-flex cursor-pointer items-center gap-1.5 rounded-lg px-3 py-1.5 transition duration-200 hover:bg-ink/10 hover:shadow-sm hover:ring-1 hover:ring-ink/10 active:scale-95";
