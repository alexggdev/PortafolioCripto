using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.Entities;

/// <summary>Último precio conocido (respaldo en SQL de la caché en memoria). Una fila por moneda.</summary>
public class AssetPrice
{
    public int AssetId { get; set; }
    public decimal PriceUsd { get; set; }
    public decimal? MarketCapUsd { get; set; }
    public decimal? Volume24hUsd { get; set; }
    public decimal? Change1hPct { get; set; }
    public decimal? Change24hPct { get; set; }
    public decimal? Change7dPct { get; set; }
    public DataSource Source { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
