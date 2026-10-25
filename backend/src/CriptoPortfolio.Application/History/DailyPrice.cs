namespace CriptoPortfolio.Application.History;

/// <summary>
/// Cierre diario de una moneda (día UTC). Open, High y Low solo existen si el proveedor los da
/// (Binance sí, CoinGecko no).
/// </summary>
public sealed record DailyPrice(
    DateOnly DateUtc,
    decimal CloseUsd,
    decimal? OpenUsd = null,
    decimal? HighUsd = null,
    decimal? LowUsd = null,
    decimal? VolumeUsd = null);
