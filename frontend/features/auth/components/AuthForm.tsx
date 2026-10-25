"use client";

import Link from "next/link";
import { useActionState } from "react";
import { loginAction, registerAction, type AuthFormState } from "@/app/actions/auth";

const INITIAL: AuthFormState = { error: null, email: "" };

const inputClass = "w-full border border-rule bg-surface px-3 py-2 text-ink placeholder:text-soft";

export function AuthForm({ mode, next, notice }: { mode: "login" | "register"; next: string; notice?: string }) {
  const [state, action, pending] = useActionState(mode === "login" ? loginAction : registerAction, INITIAL);
  const isLogin = mode === "login";
  const nextQuery = next ? `?next=${encodeURIComponent(next)}` : "";

  return (
    <form action={action} className="mx-auto w-full max-w-sm space-y-4" noValidate>
      <h1 className="condensed text-3xl font-bold tracking-tight">{isLogin ? "Entrar" : "Crear cuenta"}</h1>
      <p className="text-sm text-soft">
        {isLogin
          ? "Entra para ver tu portafolio."
          : "Tu portafolio es un diario manual de tus monedas: no compra ni vende nada por ti."}
      </p>

      {notice && (
        <p role="status" className="border border-rule bg-surface px-3 py-2 text-sm">
          {notice}
        </p>
      )}

      <input type="hidden" name="next" value={next} />

      <div className="space-y-1">
        <label htmlFor="email" className="block text-sm font-medium">
          Email
        </label>
        <input
          id="email"
          name="email"
          type="email"
          required
          maxLength={256}
          autoComplete="email"
          defaultValue={state.email}
          className={inputClass}
        />
      </div>

      <div className="space-y-1">
        <label htmlFor="password" className="block text-sm font-medium">
          Contraseña
        </label>
        <input
          id="password"
          name="password"
          type="password"
          required
          maxLength={128}
          autoComplete={isLogin ? "current-password" : "new-password"}
          aria-describedby={isLogin ? undefined : "password-help"}
          className={inputClass}
        />
        {!isLogin && (
          <p id="password-help" className="text-sm text-soft">
            Mínimo 8 caracteres, con mayúscula, minúscula y número.
          </p>
        )}
      </div>

      {!isLogin && (
        <div className="space-y-1">
          <label htmlFor="confirm" className="block text-sm font-medium">
            Repite la contraseña
          </label>
          <input
            id="confirm"
            name="confirm"
            type="password"
            required
            maxLength={128}
            autoComplete="new-password"
            className={inputClass}
          />
        </div>
      )}

      {state.error && (
        <p role="alert" className="text-sm text-down">
          {state.error}
        </p>
      )}

      <button
        type="submit"
        disabled={pending}
        className="w-full bg-accent px-4 py-2 font-medium text-on-accent disabled:opacity-60"
      >
        {pending ? "Un momento…" : isLogin ? "Entrar" : "Crear cuenta"}
      </button>

      <p className="text-sm text-soft">
        {isLogin ? "¿No tienes cuenta? " : "¿Ya tienes cuenta? "}
        <Link href={`${isLogin ? "/registro" : "/login"}${nextQuery}`} className="underline underline-offset-4 hover:text-ink">
          {isLogin ? "Crear cuenta" : "Entrar"}
        </Link>
      </p>
    </form>
  );
}
