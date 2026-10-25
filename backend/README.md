# Backend · CriptoPortfolio API

API REST en **ASP.NET Core 8** con **Entity Framework Core** y **SQL Server**. Gestiona usuarios, portafolios y transacciones, calcula posiciones con coste FIFO y mantiene al día precios, histórico y tipos de cambio mediante servicios en segundo plano.

## Estructura

```
backend/
├── CriptoPortfolio.sln
├── Directory.Build.props            net8.0, nullable e implicit usings para todos los proyectos
├── src/
│   ├── CriptoPortfolio.Domain
│   ├── CriptoPortfolio.Application
│   ├── CriptoPortfolio.Infrastructure
│   └── CriptoPortfolio.Api
└── tests/
    └── CriptoPortfolio.Tests
```

La dependencia va siempre hacia dentro: `Api → Infrastructure → Application → Domain`.

| Proyecto | Responsabilidad |
|---|---|
| **Domain** | Entidades (`Asset`, `Portfolio`, `Transaction`, `PriceAlert`...) y estrategias de coste (`FifoCostBasisStrategy`, `AverageCostBasisStrategy`). Código puro: se prueba con listas en memoria. |
| **Application** | Interfaces (`IPortfolioService`, `IAssetQueryService`, `ICatalogProvider`...), modelos de entrada y salida y validación de transacciones. |
| **Infrastructure** | `AppDbContext` y migraciones, Identity, emisión de JWT y refresh tokens, clientes de CoinGecko, Binance y ExchangeRate, servicios de aplicación y workers. |
| **Api** | Controladores, autenticación JWT, límites de peticiones, CORS, Swagger y registro con Serilog. |

## Requisitos

- .NET SDK 8
- SQL Server accesible en local

## Configuración

Los valores por defecto están en `src/CriptoPortfolio.Api/appsettings.json`. Los secretos **no** van en el repositorio.

| Clave | Descripción |
|---|---|
| `ConnectionStrings:Default` | Cadena de SQL Server. Por defecto, `localhost` con autenticación de Windows. |
| `Jwt:SigningKey` | **Obligatoria.** Clave HMAC de al menos 32 bytes. La API no arranca sin ella. |
| `CoinGecko:ApiKey` | Clave de CoinGecko (opcional). Con plan de pago hay que cambiar también `BaseUrl` y `ApiKeyHeader`. |
| `Cors:Origins` | Orígenes permitidos. En desarrollo: `http://localhost:3000`. |
| `Database:MigrateOnStartup` | Aplica las migraciones pendientes al arrancar. Activado solo en desarrollo. |
| `Portfolio:CostBasisMethod` | `Fifo` (por defecto) o `Average`. |

Guardar los secretos en local:

```powershell
cd src\CriptoPortfolio.Api
dotnet user-secrets set "Jwt:SigningKey" "<clave aleatoria de al menos 32 caracteres>"
dotnet user-secrets set "CoinGecko:ApiKey" "<tu clave de CoinGecko>"
```

Para generar una clave aleatoria en PowerShell:

```powershell
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

En otros entornos se usan variables de entorno con doble guion bajo como separador: `Jwt__SigningKey`, `CoinGecko__ApiKey`, `ConnectionStrings__Default`.

## Ejecución

```powershell
cd src\CriptoPortfolio.Api
dotnet run
```

- API: <http://localhost:5080>
- Swagger (solo en desarrollo): <http://localhost:5080/swagger>
- Comprobación de estado: `GET /health`

### Base de datos y migraciones

En desarrollo las migraciones se aplican solas al arrancar. Para crear una nueva migración, desde `backend`:

```powershell
dotnet ef migrations add NombreDeLaMigracion --project src\CriptoPortfolio.Infrastructure --startup-project src\CriptoPortfolio.Api
```

Las herramientas de EF usan `DesignTimeDbContextFactory`, que no arranca el host y por tanto no exige los secretos. Su cadena de conexión puede cambiarse con la variable de entorno `CRIPTO_CONNECTION`.

## Endpoints

Todos cuelgan de `/api`. Los de la zona privada requieren `Authorization: Bearer <jwt>`.

| Área | Método y ruta | Acceso |
|---|---|---|
| **Auth** | `POST /auth/register`, `POST /auth/login` | Público |
| | `POST /auth/refresh`, `POST /auth/logout` | Público |
| | `GET /auth/me` | Privado |
| **Mercado** | `GET /assets` · `GET /assets/{slug}` · `GET /assets/{slug}/chart` | Público |
| **Divisas** | `GET /fx` | Público |
| **Portafolios** | `GET /portfolios` · `POST /portfolios` · `PUT /portfolios/{id}` · `DELETE /portfolios/{id}` | Privado |
| | `POST /portfolios/{id}/duplicate` · `PUT /portfolios/order` | Privado |
| **Posiciones** | `GET /portfolio` · `GET /portfolio/overview` · `GET /portfolio/history` | Privado |
| | `DELETE /portfolio/assets/{slug}` · `POST /portfolio/assets/{slug}/move` | Privado |
| **Transacciones** | `GET /portfolio/transactions` · `POST /portfolio/transactions` | Privado |
| | `PUT /portfolio/transactions/{id}` · `DELETE /portfolio/transactions/{id}` | Privado |
| | `GET /portfolio/transactions/export?format=standard\|excel` | Privado |
| **Seguimiento** | `GET /watchlist` · `PUT /watchlist/{slug}` · `DELETE /watchlist/{slug}` | Privado |
| **Alertas** | `GET /alerts` · `POST /alerts` · `DELETE /alerts/{id}` | Privado |
| | `GET /alerts/unread-count` · `POST /alerts/read` | Privado |

Los errores se devuelven como `application/problem+json`, con los mensajes en español.

## Seguridad

- **Identity** con contraseñas de al menos 8 caracteres (mayúscula, minúscula y dígito), correo único y bloqueo de 15 minutos tras 5 intentos fallidos.
- **JWT** de 15 minutos y **refresh token** de 14 días, ambos configurables. Cada refresh token se guarda con hash en base de datos y solo puede usarse una vez: al renovarse se revoca y se emite otro.
- **Límite de peticiones** por ventana de un minuto: registro e inicio de sesión, 10 por IP; lectura pública, 120 por IP; zona privada, 120 por usuario.
- Detrás de Next.js, la IP del visitante llega en `X-Forwarded-For` y solo se acepta si la petición procede de localhost.

## Servicios en segundo plano

Cada worker se puede desactivar con `Enabled: false` en su sección de configuración. Los que escriben datos compartidos usan un bloqueo en base de datos para no solaparse entre ejecuciones.

| Worker | Qué hace | Frecuencia por defecto |
|---|---|---|
| `CatalogSyncWorker` | Sincroniza el catálogo y los precios de las 500 monedas principales desde CoinGecko. | 15 min |
| `LivePriceWorker` | Actualiza el precio en tiempo casi real desde Binance y lo mantiene en caché. | 5 s |
| `HistorySyncWorker` | Descarga el histórico diario (Binance y CoinGecko) para gráficos y valoración histórica. | 10 min |
| `FxRefreshWorker` | Actualiza los tipos de cambio USD → EUR, PEN y CLP. | 6 h |
| `PortfolioSnapshotWorker` | Calcula y guarda el valor diario de cada portafolio para el gráfico de evolución. | 30 min |
| `PriceAlertWorker` | Evalúa las alertas de precio y marca las que se cumplen. | 30 s |

## Modelo de transacciones

El dominio admite `Buy`, `Sell`, `TransferIn`, `TransferOut`, `Staking`, `Airdrop` y `Fee`. El cálculo de coste sigue estas reglas:

- Compras y transferencias entrantes entran a su precio más comisión.
- `Staking` y `Airdrop` entran a coste cero.
- Una venta consume lotes por orden de entrada (FIFO) y la ganancia es el ingreso menos comisión menos el coste de lo vendido.
- Una transferencia saliente retira unidades con su coste, sin generar ganancia ni pérdida.
- Si una salida supera lo que había en cartera, la operación se rechaza.

## Pruebas

```powershell
cd backend
dotnet test
```

Las pruebas usan **xUnit**, **SQLite en memoria** y `TimeProvider` de pruebas, así que no requieren SQL Server ni acceso a internet. Cubren el cálculo de coste, la validación y el CSV, la autenticación y los JWT, los servicios de portafolio, alertas y seguimiento, la sincronización y los clientes de los proveedores externos.
