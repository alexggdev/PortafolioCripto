namespace CriptoPortfolio.Domain;

/// <summary>Nombres de las tareas que usan SyncState como candado.</summary>
public static class SyncJobNames
{
    public const string Catalog = "catalog";
    public const string LivePrices = "live-prices";
    public const string BinanceMapping = "binance-mapping";
    public const string DailyHistory = "daily-history";
    public const string PortfolioSnapshots = "portfolio-snapshots";
    public const string PriceAlerts = "price-alerts";
}
