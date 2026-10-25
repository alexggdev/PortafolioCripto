using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Abstractions;

/// <summary>
/// Lectura del catálogo para la API pública. Solo usa SQL y la caché en memoria:
/// nunca llama a CoinGecko ni a Binance.
/// </summary>
public interface IAssetQueryService
{
    Task<PagedResult<AssetSummary>> ListAsync(AssetListQuery query, CancellationToken ct = default);

    /// <summary>Null si el slug no existe o la moneda está inactiva.</summary>
    Task<AssetSummary?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Monedas activas con esos ids (las inactivas o inexistentes se omiten). Sin orden garantizado.</summary>
    Task<IReadOnlyList<AssetSummary>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}
