# Portafolio Cripto

Diario manual de portafolios de criptomonedas con datos de CoinGecko y Binance.

- Backend: ASP.NET Core 8, EF Core y SQL Server
- Frontend: Next.js 16, React 19 y Tailwind 4

## Cómo arrancar
1. Backend: guarda `Jwt:SigningKey` y `CoinGecko:ApiKey` con `dotnet user-secrets set`, y ejecuta `dotnet run` en `backend/src/CriptoPortfolio.Api`.
2. Frontend: copia `frontend/.env.example` a `.env.local`, luego `npm install` y `npm run dev`.