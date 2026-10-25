using CriptoPortfolio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CriptoPortfolio.Application.Abstractions;

/// <summary>Acceso a los datos del dominio. Lo implementa AppDbContext en Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Asset> Assets { get; }
    DbSet<AssetPrice> AssetPrices { get; }
    DbSet<AssetDailyPrice> AssetDailyPrices { get; }
    DbSet<Portfolio> Portfolios { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<PortfolioSetting> PortfolioSettings { get; }
    DbSet<PortfolioDailyValue> PortfolioDailyValues { get; }
    DbSet<SyncState> SyncStates { get; }
    DbSet<WatchlistItem> WatchlistItems { get; }
    DbSet<PriceAlert> PriceAlerts { get; }

    /// <summary>Lo implementa DbContext tal cual. Permite vaciar el seguimiento en cargas largas.</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
