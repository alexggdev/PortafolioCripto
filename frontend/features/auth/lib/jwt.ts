// Solo LEE la caducidad del token para saber cuándo pedir otro inicio de sesión.
// No comprueba la firma: eso lo hace la API de .NET en cada petición.

/** Caducidad del JWT en milisegundos desde 1970, o null si el token no se entiende. */
export function jwtExpiresAtMs(token: string): number | null {
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const payload = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(padded), (c) => c.charCodeAt(0)))) as {
      exp?: unknown;
    };
    return typeof payload.exp === "number" && Number.isFinite(payload.exp) ? payload.exp * 1000 : null;
  } catch {
    return null;
  }
}

/** Caducado (o a punto de caducar, con un margen) o ilegible. */
export function isTokenExpired(token: string, nowMs: number, marginMs = 30_000): boolean {
  const expires = jwtExpiresAtMs(token);
  return expires === null || expires - marginMs <= nowMs;
}
