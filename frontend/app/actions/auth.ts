"use server";

import { redirect } from "next/navigation";
import { apiRequest } from "@/shared/lib/apiRequest";
import { safeNextPath } from "@/features/auth/lib/safeNext";
import { currentRefreshToken, endSession, startSession } from "@/features/auth/lib/session";
import type { AuthResponse } from "@/shared/lib/types";

export interface AuthFormState {
  error: string | null;
  email: string;
}

function text(data: FormData, name: string): string {
  const value = data.get(name);
  return typeof value === "string" ? value : "";
}

async function authenticate(path: "/api/auth/login" | "/api/auth/register", email: string, password: string) {
  const result = await apiRequest<AuthResponse>("POST", path, { body: JSON.stringify({ email, password }) });
  if (!result.ok || !result.data) return { error: result.errors.join(" ") };
  await startSession(result.data);
  return { error: null };
}

export async function loginAction(_previous: AuthFormState, data: FormData): Promise<AuthFormState> {
  const email = text(data, "email").trim();
  const password = text(data, "password");
  const next = safeNextPath(text(data, "next"));

  if (!email || !password) return { error: "Escribe tu email y tu contraseña.", email };

  const { error } = await authenticate("/api/auth/login", email, password);
  if (error) return { error, email };
  redirect(next);
}

export async function registerAction(_previous: AuthFormState, data: FormData): Promise<AuthFormState> {
  const email = text(data, "email").trim();
  const password = text(data, "password");
  const confirm = text(data, "confirm");
  const next = safeNextPath(text(data, "next"));

  if (!email || !password) return { error: "Escribe tu email y una contraseña.", email };
  if (password !== confirm) return { error: "Las contraseñas no coinciden.", email };

  const { error } = await authenticate("/api/auth/register", email, password);
  if (error) return { error, email };
  redirect(next);
}

export async function logoutAction() {
  // Se avisa a la API para que el refresh token deje de valer; si falla, igualmente se borran las cookies.
  const refreshToken = await currentRefreshToken();
  if (refreshToken) {
    await apiRequest("POST", "/api/auth/logout", { body: JSON.stringify({ refreshToken }) });
  }
  await endSession();
  redirect("/");
}
