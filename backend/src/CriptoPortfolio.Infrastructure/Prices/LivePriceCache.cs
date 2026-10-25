using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Prices;

namespace CriptoPortfolio.Infrastructure.Prices;

/// <summary>Caché en memoria, segura entre hilos. Es un singleton: una por proceso.</summary>
public sealed class LivePriceCache : ILivePriceCache
{
    private readonly ConcurrentDictionary<int, LivePrice> _prices = new();

    public int Count => _prices.Count;

    public void SetRange(IEnumerable<KeyValuePair<int, LivePrice>> prices)
    {
        foreach (var item in prices)
        {
            _prices[item.Key] = item.Value;
        }
    }

    public bool TryGet(int assetId, [NotNullWhen(true)] out LivePrice? price) =>
        _prices.TryGetValue(assetId, out price);

    public IReadOnlyDictionary<int, LivePrice> Snapshot() => new Dictionary<int, LivePrice>(_prices);
}
