using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.Entities;

/// <summary>Cierre diario (UTC). Clave: (AssetId, DateUtc).</summary>
public class AssetDailyPrice
{
    public int AssetId { get; set; }
    public DateOnly DateUtc { get; set; }
    public decimal CloseUsd { get; set; }
    public decimal? OpenUsd { get; set; }
    public decimal? HighUsd { get; set; }
    public decimal? LowUsd { get; set; }
    public decimal? VolumeUsd { get; set; }
    public DataSource Source { get; set; }
}
