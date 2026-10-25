# Cripto (frontend)

Next.js (App Router) + TypeScript + Tailwind 4. Lee solo de la API de .NET; nunca de CoinGecko ni de Binance.

## Arrancar

1. Instala Node 22 o superior.
2. `copy .env.example .env.local` (en PowerShell) y ajusta `API_URL` si la API no está en `http://localhost:5080`.
3. `npm install`
4. Con la API de .NET en marcha: `npm run dev` y abre http://localhost:3000

## Comandos

- `npm test`: pruebas de la lógica (formato de números, parámetros de la URL, gráfico, monedas).
- `npm run typecheck`: comprueba los tipos.
- `npm run build`: compilación de producción.

## Estructura

- `app/`: páginas y rutas. `app/api/assets/route.ts` es el proxy que usa el navegador para refrescar precios.
- `components/`: piezas de la interfaz. `MarketTable` refresca los precios cada 10 s mientras la pestaña está visible.
- `lib/`: cliente de la API (`api.ts`), parámetros de la URL (`query.ts`) y formato de números (`format.ts`).

## Moneda de visualización

Todo se guarda y calcula en USD; el selector (USD, EUR, PEN, CLP) solo convierte al mostrar. La elección va en la cookie `currency`.
Los tipos de cambio los sirve la API de .NET en `GET /api/fx` y el layout los lee en el servidor (`lib/fx.ts`). Si la API no los tiene
todavía, el selector se oculta y la web sigue en dólares.
