namespace CriptoPortfolio.Infrastructure.Providers.Binance;

public sealed class BinanceOptions
{
    public const string SectionName = "Binance";

    /// <summary>Solo datos de mercado, sin clave. Con barra final.</summary>
    public string BaseUrl { get; set; } = "https://data-api.binance.vision/api/v3/";

    public int TimeoutSeconds { get; set; } = 10;
}
