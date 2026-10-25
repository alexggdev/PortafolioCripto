using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Alerts;
using CriptoPortfolio.Application.Auth;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Application.Watchlist;
using CriptoPortfolio.Domain.CostBasis;
using CriptoPortfolio.Infrastructure.Alerts;
using CriptoPortfolio.Infrastructure.Catalog;
using CriptoPortfolio.Infrastructure.Fx;
using CriptoPortfolio.Infrastructure.Identity;
using CriptoPortfolio.Infrastructure.Jwt;
using CriptoPortfolio.Infrastructure.Persistence;
using CriptoPortfolio.Infrastructure.Portfolios;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Infrastructure.Providers.Binance;
using CriptoPortfolio.Infrastructure.Providers.CoinGecko;
using CriptoPortfolio.Infrastructure.Providers.ExchangeRate;
using CriptoPortfolio.Infrastructure.Sync;
using CriptoPortfolio.Infrastructure.Watchlist;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registro completo para la API: SQL Server + todo lo demás.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Falta ConnectionStrings:Default.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure()));

        services.AddInfrastructureCore(config);

        // Los workers solo corren en la API, no en los tests (que usan AddInfrastructureCore).
        if (config.GetValue(CatalogSyncOptions.SectionName + ":Enabled", true))
        {
            services.AddHostedService<CatalogSyncWorker>();
        }

        if (config.GetValue(LivePriceOptions.SectionName + ":Enabled", true))
        {
            services.AddHostedService<LivePriceWorker>();
        }

        if (config.GetValue(HistorySyncOptions.SectionName + ":Enabled", true))
        {
            services.AddHostedService<HistorySyncWorker>();
        }

        if (config.GetValue(FxOptions.SectionName + ":Enabled", true))
        {
            services.AddHostedService<FxRefreshWorker>();
        }

        if (config.GetValue(PortfolioHistoryOptions.SectionName + ":Enabled", true))
        {
            services.AddHostedService<PortfolioSnapshotWorker>();
        }

        if (config.GetValue(PriceAlertOptions.SectionName + ":Enabled", true))
        {
            services.AddHostedService<PriceAlertWorker>();
        }

        return services;
    }

    /// <summary>
    /// Todo menos el proveedor de base de datos. Separado para que los tests registren SQLite
    /// y usen exactamente la misma configuración de Identity, JWT y servicios.
    /// </summary>
    public static IServiceCollection AddInfrastructureCore(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .Validate(o => o.IsValid(out _),
                "Jwt no es válido: SigningKey de al menos 32 bytes, Issuer y Audience obligatorios.")
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<CoinGeckoOptions>().Bind(config.GetSection(CoinGeckoOptions.SectionName));
        services.AddHttpClient<ICatalogProvider, CoinGeckoCatalogProvider>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<CoinGeckoOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 5, 120));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CriptoPortfolio/1.0");
        });

        services.AddOptions<CatalogSyncOptions>().Bind(config.GetSection(CatalogSyncOptions.SectionName));
        services.AddScoped<ICatalogSyncService, CatalogSyncService>();

        services.AddOptions<BinanceOptions>().Bind(config.GetSection(BinanceOptions.SectionName));
        services.AddOptions<LivePriceOptions>().Bind(config.GetSection(LivePriceOptions.SectionName));
        services.AddHttpClient<IBinanceMarketClient, BinanceMarketClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 3, 60));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CriptoPortfolio/1.0");
        });
        services.AddHttpClient<IBinanceHistoryClient, BinanceHistoryClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 3, 60));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CriptoPortfolio/1.0");
        });
        services.AddHttpClient<ICoinGeckoHistoryClient, CoinGeckoHistoryClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<CoinGeckoOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 5, 120));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CriptoPortfolio/1.0");
        });
        services.AddSingleton<ILivePriceCache, LivePriceCache>();
        services.AddScoped<IBinanceMappingService, BinanceMappingService>();

        services.AddScoped<IAssetQueryService, AssetQueryService>();
        services.AddScoped<IWatchlistService, WatchlistService>();

        services.AddOptions<PriceAlertOptions>().Bind(config.GetSection(PriceAlertOptions.SectionName));
        services.AddScoped<AlertPriceReader>();
        services.AddScoped<IPriceAlertService, PriceAlertService>();
        services.AddScoped<IPriceAlertEvaluator, PriceAlertEvaluator>();
        services.AddScoped<IAssetChartService, AssetChartService>();

        services.AddOptions<HistorySyncOptions>().Bind(config.GetSection(HistorySyncOptions.SectionName));
        services.AddSingleton<HistoryRetryGate>();
        services.AddScoped<IHistorySyncService, HistorySyncService>();

        services.AddOptions<ExchangeRateOptions>().Bind(config.GetSection(ExchangeRateOptions.SectionName));
        services.AddOptions<FxOptions>().Bind(config.GetSection(FxOptions.SectionName));
        services.AddHttpClient<IFxRatesClient, ExchangeRateClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<ExchangeRateOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 3, 60));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CriptoPortfolio/1.0");
        });
        services.AddSingleton<IFxRateCache, FxRateCache>();
        services.AddScoped<IFxRefreshService, FxRefreshService>();

        services.AddOptions<PortfolioOptions>().Bind(config.GetSection(PortfolioOptions.SectionName));
        services.AddSingleton<ICostBasisStrategy>(sp =>
            sp.GetRequiredService<IOptions<PortfolioOptions>>().Value.CostBasisMethod == CostBasisMethod.Average
                ? new AverageCostBasisStrategy()
                : new FifoCostBasisStrategy());
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IPortfolioManager, PortfolioManager>();

        services.AddOptions<PortfolioHistoryOptions>().Bind(config.GetSection(PortfolioHistoryOptions.SectionName));
        services.AddScoped<PortfolioHistoryService>();
        services.AddScoped<IPortfolioHistoryService>(sp => sp.GetRequiredService<PortfolioHistoryService>());
        services.AddScoped<IPortfolioHistoryInvalidator>(sp => sp.GetRequiredService<PortfolioHistoryService>());

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ISyncLockService, SyncLockService>();

        return services;
    }
}
