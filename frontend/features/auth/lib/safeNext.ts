// Adonde se vuelve tras iniciar sesión. Solo rutas internas de esta web: nunca otra dirección.

export const DEFAULT_AFTER_LOGIN = "/portafolio";

export function safeNextPath(value: unknown, fallback: string = DEFAULT_AFTER_LOGIN): string {
  if (typeof value !== "string") return fallback;
  const path = value.trim();

  if (!path.startsWith("/")) return fallback; // https://otra.web, javascript:..., "portafolio"
  if (path.startsWith("//")) return fallback; // //otra.web es una dirección absoluta sin protocolo
  if (path.includes("\\")) return fallback; // /\otra.web: algunos navegadores lo tratan como //
  if (/[\u0000-\u001f\u007f]/.test(path)) return fallback; // saltos de línea y otros caracteres de control

  const pathname = path.split(/[?#]/)[0];
  // Volver a estas páginas crearía un bucle o llevaría a una ruta que no es una página.
  if (pathname === "/login" || pathname === "/registro" || pathname.startsWith("/api/")) return fallback;

  return path;
}
