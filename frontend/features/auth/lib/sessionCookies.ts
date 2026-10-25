// Nombres y reglas de las cookies de sesión. Sin dependencias de Next: lo usan el middleware y las pruebas.

export const TOKEN_COOKIE = "cp_session";
export const EMAIL_COOKIE = "cp_email";
export const REFRESH_COOKIE = "cp_refresh";

export interface AuthPayload {
  token: string;
  expiresAtUtc: string;
  email: string;
  refreshToken?: string;
  refreshExpiresAtUtc?: string;
}

export interface CookieToSet {
  name: string;
  value: string;
  options: { httpOnly: true; sameSite: "lax"; secure: boolean; path: "/"; maxAge: number };
}

/** Secure por defecto en producción. En local con `next start` sobre http se desactiva con COOKIE_SECURE=false. */
export function secureCookies(): boolean {
  const flag = process.env.COOKIE_SECURE;
  if (flag === "false") return false;
  if (flag === "true") return true;
  return process.env.NODE_ENV === "production";
}

/** Fecha de la API (UTC, a veces sin la Z final) a milisegundos. */
export function parseUtcMs(value: string | undefined): number {
  if (!value) return Number.NaN;
  return Date.parse(/Z|[+-]\d\d:?\d\d$/.test(value) ? value : `${value}Z`);
}

function maxAgeSeconds(expiresMs: number, fallback: number, nowMs: number): number {
  return Number.isFinite(expiresMs) ? Math.max(60, Math.floor((expiresMs - nowMs) / 1000)) : fallback;
}

/**
 * Cookies que hay que guardar tras entrar o renovar. El JWT dura minutos; el refresh token, días.
 * Si la API no mandó refresh token (versión antigua), la sesión se comporta como antes.
 */
export function cookiesFor(
  auth: AuthPayload,
  jwtExpiresMs: number | null,
  nowMs: number,
  secure: boolean,
): CookieToSet[] {
  const base = { httpOnly: true as const, sameSite: "lax" as const, secure, path: "/" as const };
  const sessionExpires = jwtExpiresMs ?? parseUtcMs(auth.expiresAtUtc);
  const sessionAge = maxAgeSeconds(sessionExpires, 900, nowMs);

  const list: CookieToSet[] = [
    { name: TOKEN_COOKIE, value: auth.token, options: { ...base, maxAge: sessionAge } },
  ];

  if (auth.refreshToken) {
    const refreshAge = maxAgeSeconds(parseUtcMs(auth.refreshExpiresAtUtc), 14 * 24 * 3600, nowMs);
    list.push({ name: REFRESH_COOKIE, value: auth.refreshToken, options: { ...base, maxAge: refreshAge } });
    // El email acompaña al refresh token para que la cabecera siga mostrándolo cuando el JWT ya no está.
    list.push({ name: EMAIL_COOKIE, value: auth.email, options: { ...base, maxAge: refreshAge } });
  } else {
    list.push({ name: EMAIL_COOKIE, value: auth.email, options: { ...base, maxAge: sessionAge } });
  }
  return list;
}

/** Añade o reemplaza cookies en una cabecera Cookie (para que la página que se renderiza ya vea las nuevas). */
export function mergeCookieHeader(header: string | null | undefined, updates: Record<string, string>): string {
  const kept = (header ?? "")
    .split(";")
    .map((part) => part.trim())
    .filter((part) => part.length > 0 && !(part.split("=")[0]!.trim() in updates));
  for (const [name, value] of Object.entries(updates)) kept.push(`${name}=${encodeURIComponent(value)}`);
  return kept.join("; ");
}

/** Quita cookies de una cabecera Cookie. */
export function removeCookies(header: string | null | undefined, names: string[]): string {
  return (header ?? "")
    .split(";")
    .map((part) => part.trim())
    .filter((part) => part.length > 0 && !names.includes(part.split("=")[0]!.trim()))
    .join("; ");
}

/** ¿Hace falta pedir un JWT nuevo? Sí si falta o caduca en menos de `marginMs` y hay refresh token. */
export function needsRefresh(
  jwt: string | undefined,
  refreshToken: string | undefined,
  isExpired: (token: string) => boolean,
): boolean {
  if (!refreshToken) return false;
  return !jwt || isExpired(jwt);
}
