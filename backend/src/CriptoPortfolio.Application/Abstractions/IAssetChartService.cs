using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Abstractions;

public interface IAssetChartService
{
    /// <summary>Null si el slug no existe o la moneda está inactiva. Solo lee de SQL y de la caché en vivo.</summary>
    Task<AssetChart?> GetChartAsync(string slug, ChartRange range, CancellationToken ct = default);
}
