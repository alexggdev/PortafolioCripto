# Frontend · Cripto

Aplicación web con **Next.js 16** (App Router), **React 19**, **TypeScript** y **Tailwind CSS 4**. Es la interfaz del portafolio de criptomonedas y solo lee datos de la API de .NET.

## Requisitos

- Node.js 22 o superior
- La API de .NET en marcha (por defecto en `http://localhost:5080`)

## Puesta en marcha

En Windows PowerShell:

```powershell
copy .env.example .env.local
npm install
npm run dev
```

Abre <http://localhost:3000>.

### Variables de entorno

| Variable | Descripción |
|---|---|
| `API_URL` | Dirección de la API de .NET. Solo la usa el servidor de Next; el navegador nunca la ve. Por defecto, `http://localhost:5080`. |
| `COOKIE_SECURE` | En producción las cookies de sesión son `Secure` (solo HTTPS). Para probar `next start` en local con `http://localhost`, ponla en `false`. |

## Scripts

| Comando | Qué hace |
|---|---|
| `npm run dev` | Servidor de desarrollo |
| `npm run build` | Compilación de producción |
| `npm start` | Sirve la compilación de producción |
| `npm run typecheck` | Comprueba los tipos con TypeScript |
| `npm test` | Pruebas de la lógica con el ejecutor nativo de Node (`node:test` y `tsx`) |

## Estructura

```
frontend/
├── app/            Rutas y route handlers
├── features/       Código organizado por funcionalidad
│   ├── portfolio/  Posiciones, transacciones, gráficos, exportación
│   ├── watchlist/  Seguimiento de monedas
│   ├── alerts/     Alertas de precio y campana de avisos
│   ├── auth/       Sesión, formularios y cookies
│   ├── market/     Tabla de mercado, ficha de moneda y gráfico de precio
│   └── currency/   Selector de moneda y conversión
├── shared/         Componentes y utilidades comunes
├── tests/          Pruebas de la lógica
└── proxy.ts        Renovación de la sesión
```

Dentro de cada funcionalidad, `components/` contiene la interfaz y `lib/` la lógica. La lógica de `lib/` es código puro, sin React, para poder probarla de forma aislada.

### Rutas

| Ruta | Contenido |
|---|---|
| `/` | Mercado: precios, variaciones y búsqueda |
| `/monedas/[slug]` | Ficha de una moneda con su gráfico |
| `/portafolio` | Toda la sección del portafolio |
| `/seguimiento` | Monedas seguidas |
| `/alertas` | Alertas de precio |
| `/login`, `/registro` | Acceso |

La sección del portafolio vive en una sola URL. La pantalla activa (vista general, un portafolio o una moneda) y el portafolio abierto se guardan en cookies, no en la dirección.

## Cómo se comunica con la API

El navegador nunca llama a la API de .NET directamente. Los route handlers de `app/api/` actúan como proxy: reciben la petición, leen el JWT de la cookie, lo reenvían a la API y devuelven la respuesta. El servidor de Next reenvía también `X-Forwarded-For` para que el límite de peticiones de la API vea la IP real del visitante.

### Sesión

- El JWT y el refresh token se guardan en cookies `httpOnly`.
- `proxy.ts` (antes `middleware.ts`) renueva el JWT con el refresh token antes de que caduque. Como cada refresh token vale una sola vez, las peticiones simultáneas comparten el resultado de una única renovación.
- Si el refresh token ya no es válido, se limpian las cookies y la página redirige al acceso.

## Moneda de visualización

Todo se guarda y calcula en USD. El selector (USD, EUR, PEN, CLP) solo convierte al mostrar y su elección se guarda en la cookie `currency`. Los tipos de cambio los sirve la API en `GET /api/fx` y el layout los lee en el servidor. Si la API aún no los tiene, el selector se oculta y la web sigue en dólares.

## Actualización de precios

Las tablas refrescan los precios cada pocos segundos mientras la pestaña está visible, a través de los route handlers de `app/api/`.
