import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import { cookies } from "next/headers";
import type { ReactNode } from "react";
import Link from "next/link";
import { AlertsBell } from "@/features/alerts/components/AlertsBell";
import { PieIcon } from "@/shared/components/Icons";
import { CurrencyProvider } from "@/features/currency/components/CurrencyProvider";
import { CurrencySelect } from "@/features/currency/components/CurrencySelect";
import { ThemeToggle } from "@/shared/components/ThemeToggle";
import { Toaster } from "@/shared/components/Toaster";
import { UserMenu } from "@/features/auth/components/UserMenu";
import { WatchlistMenu } from "@/features/watchlist/components/WatchlistMenu";
import { CURRENCY_COOKIE, parseCurrency } from "@/features/currency/lib/currency";
import { loadRates } from "@/features/currency/lib/fx";
import { getSession } from "@/features/auth/lib/session";
import { NAV_LINK } from "@/shared/lib/ui";
import "./globals.css";

// Una sola familia para todo el sitio (la misma que usa CoinMarketCap).
const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: "Cripto: precios de criptomonedas",
    template: "%s · Cripto",
  },
  description: "Precios, variaciones y gráficos de las principales criptomonedas, en dólares, euros, soles o pesos chilenos.",
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#f8fafd" },
    { media: "(prefers-color-scheme: dark)", color: "#0d1421" },
  ],
};

export default async function RootLayout({ children }: { children: ReactNode }) {
  const jar = await cookies();
  const stored = jar.get("theme")?.value;
  const requested = parseCurrency(jar.get(CURRENCY_COOKIE)?.value);
  const fx = await loadRates();
  const session = await getSession();
  const theme = stored === "light" || stored === "dark" ? stored : undefined;

  return (
    // suppressHydrationWarning: extensiones del navegador (carteras cripto, etc.) añaden atributos a <html> antes de que cargue React.
    <html lang="es" data-theme={theme} className={inter.variable} suppressHydrationWarning>
      <body className="flex min-h-dvh flex-col font-sans antialiased">
        <CurrencyProvider requested={requested} fx={fx}>
        <header className="mx-auto flex w-full max-w-[120rem] items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
          <Link href="/" className="condensed text-2xl font-bold tracking-tight">
            Cripto
          </Link>
          <nav aria-label="Principal" className="flex flex-wrap items-center justify-end gap-x-4 gap-y-2 text-sm">
            <Link href="/portafolio/abrir" prefetch={false} className={NAV_LINK}>
              <PieIcon className="size-4" /> Mi portafolio
            </Link>
            <WatchlistMenu signedIn={session !== null} />
            {session && <AlertsBell />}
            {session ? (
              <UserMenu email={session.email} />
            ) : (
              <>
                <Link href="/login" className={NAV_LINK}>
                  Entrar
                </Link>
                <CurrencySelect />
                <ThemeToggle />
              </>
            )}
          </nav>
        </header>

        <main className="mx-auto w-full max-w-[120rem] flex-1 px-4 pb-16 sm:px-6 lg:px-8">{children}</main>

        <footer className="mx-auto w-full max-w-[120rem] px-4 py-8 text-sm text-soft sm:px-6 lg:px-8">
          <p>
            Datos de mercado de{" "}
            <a href="https://www.coingecko.com" className="underline underline-offset-4 hover:text-ink" rel="noopener noreferrer">
              CoinGecko
            </a>{" "}
            y Binance; tipos de cambio de{" "}
            <a href="https://www.exchangerate-api.com" className="underline underline-offset-4 hover:text-ink" rel="noopener noreferrer">
              ExchangeRate-API
            </a>
            . Los precios son orientativos y no son asesoramiento financiero.
          </p>
        </footer>
        <Toaster />
        </CurrencyProvider>
      </body>
    </html>
  );
}
