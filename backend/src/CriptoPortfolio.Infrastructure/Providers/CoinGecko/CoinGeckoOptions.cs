namespace CriptoPortfolio.Infrastructure.Providers.CoinGecko;

public sealed class CoinGeckoOptions
{
    public const string SectionName = "CoinGecko";

    /// <summary>Plan Demo. Con un plan de pago cambia a https://pro-api.coingecko.com/api/v3/ y a su cabecera.</summary>
    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3/";

    /// <summary>Clave de API. Nunca en el repositorio: user-secrets o variable CoinGecko__ApiKey.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Cabecera de la clave. Demo: x-cg-demo-api-key. Pro: x-cg-pro-api-key.</summary>
    public string ApiKeyHeader { get; set; } = "x-cg-demo-api-key";

    /// <summary>Monedas por llamada (máximo 250 en CoinGecko).</summary>
    public int PageSize { get; set; } = 250;

    public int TimeoutSeconds { get; set; } = 30;
}
