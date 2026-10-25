import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthForm } from "@/features/auth/components/AuthForm";
import { safeNextPath } from "@/features/auth/lib/safeNext";
import { getSession } from "@/features/auth/lib/session";

export const metadata: Metadata = { title: "Entrar", robots: { index: false } };

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };

export default async function LoginPage({ searchParams }: Props) {
  const raw = await searchParams;
  const next = safeNextPath(Array.isArray(raw.next) ? raw.next[0] : raw.next);
  const expired = raw.expired === "1";

  if (!expired && (await getSession())) redirect(next);

  return (
    <div className="pt-8">
      <AuthForm mode="login" next={next} notice={expired ? "Tu sesión ha caducado. Vuelve a entrar." : undefined} />
    </div>
  );
}
