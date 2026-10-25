using System.Diagnostics.CodeAnalysis;
using CriptoPortfolio.Application.Prices;

namespace CriptoPortfolio.Application.Abstractions;

/// <summary>
/// Precios en vivo en memoria (una entrada por moneda). Los llena el worker; las peticiones de los usuarios
/// solo leen de aquí, nunca llaman a Binance.
/// </summary>
public interface ILivePriceCache
{
    int Count { get; }

    void SetRange(IEnumerable<KeyValuePair<int, LivePrice>> prices);

    bool TryGet(int assetId, [NotNullWhen(true)] out LivePrice? price);

    /// <summary>Copia de todos los precios actuales (para listar el catálogo).</summary>
    IReadOnlyDictionary<int, LivePrice> Snapshot();
}
