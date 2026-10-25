import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { isTokenExpired, jwtExpiresAtMs } from "./jwt";
import { cookiesFor, EMAIL_COOKIE, REFRESH_COOKIE, secureCookies, TOKEN_COOKIE, type AuthPayload } from "./sessionCookies";

// La sesión son dos cookies httpOnly: el JWT de la API (dura minutos) y un refresh token (dura días).
// El JavaScript del navegador no puede leerlas. Solo el servidor de Next las lee y las adjunta a las peticiones.
// El JWT lo renueva solo el middleware (middleware.ts) con el refresh token, sin que el usuario tenga que volver a entrar.

export { EMAIL_COOKIE, REFRESH_COOKIE, TOKEN_COOKIE };

export interface Session {
  token: string;
  email: string;
}

export async function getSession(): Promise<Session | null> {
  const jar = await cookies();
  const token = jar.get(TOKEN_COOKIE)?.value;
  if (!token || isTokenExpired(token, Date.now())) return null;
  return { token, email: jar.get(EMAIL_COOKIE)?.value ?? "" };
}

export async function startSession(auth: AuthPayload) {
  const jar = await cookies();
  for (const c of cookiesFor(auth, jwtExpiresAtMs(auth.token), Date.now(), secureCookies())) {
    jar.set(c.name, c.value, c.options);
  }
}

/** Refresh token de la sesión actual (para avisar a la API al cerrar sesión). */
export async function currentRefreshToken(): Promise<string | null> {
  const jar = await cookies();
  return jar.get(REFRESH_COOKIE)?.value ?? null;
}

export async function endSession() {
  const jar = await cookies();
  jar.delete(TOKEN_COOKIE);
  jar.delete(EMAIL_COOKIE);
  jar.delete(REFRESH_COOKIE);
}

/** Para páginas privadas: sin sesión válida lleva al login y, tras entrar, de vuelta aquí. */
export async function requireSession(nextPath: string): Promise<Session> {
  const session = await getSession();
  if (!session) redirect(`/login?next=${encodeURIComponent(nextPath)}`);
  return session;
}
