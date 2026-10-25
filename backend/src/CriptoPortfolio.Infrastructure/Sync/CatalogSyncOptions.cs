namespace CriptoPortfolio.Infrastructure.Sync;

public sealed class CatalogSyncOptions
{
    public const string SectionName = "CatalogSync";

    /// <summary>Si es false, el worker no se registra (útil en tests o con varias instancias).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Monedas del catálogo (top por capitalización).</summary>
    public int TopCount { get; set; } = 500;

    /// <summary>Cada cuánto se refresca. 15 min con el top 500 gasta unos 5.760 créditos/mes en CoinGecko Demo.</summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>Duración del candado; debe ser mayor que lo que tarda una pasada.</summary>
    public int LeaseMinutes { get; set; } = 10;

    /// <summary>Espera tras arrancar la API antes de la primera pasada.</summary>
    public int StartupDelaySeconds { get; set; } = 5;
}
