using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Infrastructure.Prices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Alerts;

/// <summary>
/// Precio actual de varias monedas para las alertas: primero la caché en vivo; si está vieja, el último precio de SQL
/// siempre que sea reciente. Una moneda sin precio fiable no aparece en el resultado (no se evalúa ni se crea alerta).
/// </summary>
public sealed class AlertPriceReader
{
    private readonly IAppDbContext _db;
    private readonly ILivePriceCache _cache;
    private readonly TimeProvider _time;
    private readonly LivePriceOptions _live;
    private readonly PriceAlertOptions _options;

    public AlertPriceReader(
        IAppDbContext db,
        ILivePriceCache cache,
        TimeProvider time,
        IOptions<LivePriceOptions> live,
        IOptions<PriceAlertOptions> options)
    {
        _db = db;
        _cache = cache;
        _time = time;
        _live = live.Value;
        _options = options.Value;
    }

    public async Task<Dictionary<int, decimal>> GetPricesAsync(IReadOnlyCollection<int> assetIds, CancellationToken ct)
    {
        var result = new Dictionary<int, decimal>();
        if (assetIds.Count == 0)
        {
            return result;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var liveMax = TimeSpan.FromSeconds(Math.Max(1, _live.StaleSeconds));
        var missing = new List<int>();

        foreach (var id in assetIds)
        {
            if (_cache.TryGet(id, out var live) && live.PriceUsd > 0 && now - live.UpdatedAtUtc <= liveMax)
            {
                result[id] = live.PriceUsd;
            }
            else
            {
                missing.Add(id);
            }
        }

        if (missing.Count > 0)
        {
            var oldest = now.AddMinutes(-Math.Max(1, _options.MaxDelayedPriceMinutes));
            var rows = await _db.AssetPrices.AsNoTracking()
                .Where(p => missing.Contains(p.AssetId))
                .Select(p => new { p.AssetId, p.PriceUsd, p.UpdatedAtUtc })
                .ToListAsync(ct);

            // La comparación se hace en memoria: SQLite no compara decimales ni fechas de forma fiable.
            foreach (var row in rows)
            {
                if (row.PriceUsd > 0 && row.UpdatedAtUtc >= oldest)
                {
                    result[row.AssetId] = row.PriceUsd;
                }
            }
        }

        return result;
    }
}
