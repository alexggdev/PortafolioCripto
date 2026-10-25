using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CriptoPortfolio.Infrastructure.Catalog;

public sealed class AssetChartService : IAssetChartService
{
    /// <summary>Tope de puntos por respuesta. "Todo" de una moneda con años de historia se reduce a este número.</summary>
    public const int MaxPoints = 500;

    private readonly IAppDbContext _db;
    private readonly IAssetQueryService _assets;
    private readonly TimeProvider _time;

    public AssetChartService(IAppDbContext db, IAssetQueryService assets, TimeProvider time)
    {
        _db = db;
        _assets = assets;
        _time = time;
    }

    public async Task<AssetChart?> GetChartAsync(string slug, ChartRange range, CancellationToken ct = default)
    {
        // Reutiliza la consulta del catálogo: da el id y el precio actual (en vivo o, si caducó, el de SQL).
        var asset = await _assets.GetBySlugAsync(slug, ct);
        if (asset is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        var query = _db.AssetDailyPrices.AsNoTracking().Where(d => d.AssetId == asset.Id);
        if (ChartRanges.Days(range) is { } days)
        {
            var from = today.AddDays(-days);
            query = query.Where(d => d.DateUtc >= from);
        }

        var rows = await query
            .OrderBy(d => d.DateUtc)
            .Select(d => new { d.DateUtc, d.CloseUsd })
            .ToListAsync(ct);

        var points = rows
            .Where(r => r.DateUtc < today) // el día en curso lo pone el precio actual, no un cierre
            .Select(r => new ChartPoint(r.DateUtc, r.CloseUsd))
            .ToList();

        var includesCurrent = false;
        if (asset.PriceUsd is { } current && current > 0)
        {
            points.Add(new ChartPoint(today, current));
            includesCurrent = true;
        }

        decimal? change = null;
        if (points.Count >= 2 && points[0].PriceUsd > 0)
        {
            change = Math.Round((points[^1].PriceUsd - points[0].PriceUsd) / points[0].PriceUsd * 100m, 2);
        }

        return new AssetChart(asset.Slug, ChartRanges.Code(range), Downsample(points), change, includesCurrent);
    }

    // Reparte los puntos por igual conservando siempre el primero y el último.
    private static IReadOnlyList<ChartPoint> Downsample(List<ChartPoint> points)
    {
        if (points.Count <= MaxPoints)
        {
            return points;
        }

        var result = new List<ChartPoint>(MaxPoints);
        var last = points.Count - 1;
        var previous = -1;

        for (var i = 0; i < MaxPoints; i++)
        {
            var index = (int)Math.Round(i * (double)last / (MaxPoints - 1));
            if (index != previous)
            {
                result.Add(points[index]);
                previous = index;
            }
        }

        return result;
    }
}
