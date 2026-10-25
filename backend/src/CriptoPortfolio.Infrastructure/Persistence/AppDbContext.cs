using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CriptoPortfolio.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetPrice> AssetPrices => Set<AssetPrice>();
    public DbSet<AssetDailyPrice> AssetDailyPrices => Set<AssetDailyPrice>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PortfolioSetting> PortfolioSettings => Set<PortfolioSetting>();
    public DbSet<PortfolioDailyValue> PortfolioDailyValues => Set<PortfolioDailyValue>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // OJO: SqlClient convierte cada decimal a la escala de su columna pasando por System.Decimal, que solo
        // admite unos 28 dígitos. Con decimal(38,18) cualquier valor de más de 10 cifras enteras (la capitalización
        // de Bitcoin, 1,6 billones) lanza "Conversion overflows". Por eso el máximo son 28 dígitos en total.
        // Por defecto: importes en USD y similares, 8 decimales (20 cifras enteras).
        // Los precios unitarios (18 decimales) y las cantidades (12) se ajustan en sus configuraciones;
        // los porcentajes son (18,6).
        configurationBuilder.Properties<decimal>().HavePrecision(28, 8);
        configurationBuilder.Properties<decimal?>().HavePrecision(28, 8);

        // Todas las fechas en UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
