import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthForm } from "@/features/auth/components/AuthForm";
import { safeNextPath } from "@/features/auth/lib/safeNext";
import { getSession } from "@/features/auth/lib/session";

export const metadata: Metadata = { title: "Crear cuenta", robots: { index: false } };

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };

export default async function RegisterPage({ searchParams }: Props) {
  const raw = await searchParams;
  const next = safeNextPath(Array.isArray(raw.next) ? raw.next[0] : raw.next);

  if (await getSession()) redirect(next);

  return (
    <div className="pt-8">
      <AuthForm mode="register" next={next} />
    </div>
  );
}
