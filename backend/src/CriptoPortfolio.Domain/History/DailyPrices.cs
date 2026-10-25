namespace CriptoPortfolio.Domain.History;

/// <summary>Precio de cierre de una moneda en un día (USD).</summary>
public interface IDailyPrices
{
    /// <summary>El cierre de ese día o, si no hay, el último anterior. Null si la moneda no tiene ningún cierre hasta esa fecha.</summary>
    decimal? PriceOn(int assetId, DateOnly date);
}

/// <summary>Cierres diarios de una moneda, ordenados por fecha. Ignora precios que no sean positivos.</summary>
public sealed class DailyCloseSeries
{
    private readonly DateOnly[] _dates;
    private readonly decimal[] _closes;

    public DailyCloseSeries(IEnumerable<KeyValuePair<DateOnly, decimal>> rows)
    {
        var ordered = rows
            .Where(r => r.Value > 0m)
            .GroupBy(r => r.Key)
            .Select(g => g.Last())
            .OrderBy(r => r.Key)
            .ToArray();

        _dates = ordered.Select(r => r.Key).ToArray();
        _closes = ordered.Select(r => r.Value).ToArray();
    }

    public int Count => _dates.Length;

    public DateOnly? LastDate => _dates.Length == 0 ? null : _dates[^1];

    public decimal? OnOrBefore(DateOnly date)
    {
        var index = Array.BinarySearch(_dates, date);
        if (index >= 0)
        {
            return _closes[index];
        }

        var before = ~index - 1;
        return before < 0 ? null : _closes[before];
    }
}

/// <summary>Los cierres de varias monedas.</summary>
public sealed class DailyPriceBook : IDailyPrices
{
    private readonly Dictionary<int, DailyCloseSeries> _series = new();

    public void Add(int assetId, DailyCloseSeries series) => _series[assetId] = series;

    public DateOnly? LastDate(int assetId) => _series.TryGetValue(assetId, out var s) ? s.LastDate : null;

    public decimal? PriceOn(int assetId, DateOnly date) =>
        _series.TryGetValue(assetId, out var s) ? s.OnOrBefore(date) : null;
}

/// <summary>Sustituye el precio de un día concreto (el de hoy) por el precio actual de cada moneda.</summary>
public sealed class OverlayPrices : IDailyPrices
{
    private readonly IDailyPrices _inner;
    private readonly DateOnly _day;
    private readonly IReadOnlyDictionary<int, decimal> _overrides;

    public OverlayPrices(IDailyPrices inner, DateOnly day, IReadOnlyDictionary<int, decimal> overrides)
    {
        _inner = inner;
        _day = day;
        _overrides = overrides;
    }

    public decimal? PriceOn(int assetId, DateOnly date) =>
        date == _day && _overrides.TryGetValue(assetId, out var price) && price > 0m
            ? price
            : _inner.PriceOn(assetId, date);
}
