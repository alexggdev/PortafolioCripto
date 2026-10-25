namespace CriptoPortfolio.Infrastructure.Prices;

public sealed class LivePriceOptions
{
    public const string SectionName = "LivePrices";

    /// <summary>Si es false, el worker no se registra.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuántos segundos se leen los precios de Binance.</summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>Cada cuántos minutos se revisa qué moneda corresponde a qué par.</summary>
    public int MappingMinutes { get; set; } = 10;

    public int MappingLeaseMinutes { get; set; } = 5;

    /// <summary>Espera tras arrancar (deja que termine la primera pasada del catálogo).</summary>
    public int StartupDelaySeconds { get; set; } = 20;

    /// <summary>
    /// Para asignar un par nuevo, el precio de Binance no puede diferir más de este porcentaje (0,25 = 25 %)
    /// del de CoinGecko. Evita ligar una moneda con otra que comparte símbolo.
    /// </summary>
    public decimal MaxPriceDeviation { get; set; } = 0.25m;

    /// <summary>
    /// Si el precio en vivo tiene más de estos segundos, la API lo da por caducado y responde con el último
    /// precio de CoinGecko guardado en SQL (marcado como "delayed").
    /// </summary>
    public int StaleSeconds { get; set; } = 60;
}
