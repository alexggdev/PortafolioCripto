using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Abstractions;

/// <summary>
/// Fuente del catálogo (ranking, capitalización, imagen, variaciones). Solo la usan los workers:
/// ninguna petición de un usuario llama al proveedor.
/// </summary>
public interface ICatalogProvider
{
    /// <summary>Las <paramref name="count"/> monedas de mayor capitalización, ordenadas por ranking.</summary>
    Task<IReadOnlyList<CatalogCoin>> GetTopCoinsAsync(int count, CancellationToken ct = default);
}
