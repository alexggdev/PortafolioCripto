namespace CriptoPortfolio.Infrastructure.Providers.ExchangeRate;

public sealed class ExchangeRateOptions
{
    public const string SectionName = "ExchangeRate";

    /// <summary>open.er-api.com: gratuito y sin clave (el proveedor actualiza una vez al día). Con barra final.</summary>
    public string BaseUrl { get; set; } = "https://open.er-api.com/v6/";

    public int TimeoutSeconds { get; set; } = 10;
}
