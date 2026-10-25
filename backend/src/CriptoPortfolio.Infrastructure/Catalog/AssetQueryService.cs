using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Prices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Catalog;

public sealed class AssetQueryService : IAssetQueryService
{
    private readonly IAppDbContext _db;
    private readonly ILivePriceCache _cache;
    private readonly TimeProvider _time;
    private readonly LivePriceOptions _options;

    public AssetQueryService(
        IAppDbContext db,
        ILivePriceCache cache,
        TimeProvider time,
        IOptions<LivePriceOptions> options)
    {
        _db = db;
        _cache = cache;
        _time = time;
        _options = options.Value;
    }

    public async Task<PagedResult<AssetSummary>> ListAsync(AssetListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, AssetListQuery.MaxPageSize);

        var assets = _db.Assets.AsNoTracking().Where(a => a.IsActive);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (search.Length > AssetListQuery.MaxSearchLength)
            {
                search = search[..AssetListQuery.MaxSearchLength];
            }

            // LIKE no distingue mayúsculas en SQL Server ni en SQLite (Contains en SQLite sí las distingue).
            var pattern = "%" + EscapeLike(search) + "%";
            assets = assets.Where(a =>
                EF.Functions.Like(a.Name, pattern, "\\") || EF.Functions.Like(a.Symbol, pattern, "\\"));
        }

        var total = await assets.CountAsync(ct);

        var rows = await Sort(assets, query.Sort, query.Descending)
            .Include(a => a.Price)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var now = _time.GetUtcNow().UtcDateTime;
        var items = rows.Select(a => ToSummary(a, now)).ToList();

        return new PagedResult<AssetSummary>(items, page, pageSize, total);
    }

    public async Task<AssetSummary?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();

        var asset = await _db.Assets.AsNoTracking()
            .Include(a => a.Price)
            .FirstOrDefaultAsync(a => a.IsActive && a.Slug == normalized, ct);

        return asset is null ? null : ToSummary(asset, _time.GetUtcNow().UtcDateTime);
    }

    public async Task<IReadOnlyList<AssetSummary>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<AssetSummary>();
        }

        var assets = await _db.Assets.AsNoTracking()
            .Include(a => a.Price)
            .Where(a => a.IsActive && ids.Contains(a.Id))
            .ToListAsync(ct);

        var now = _time.GetUtcNow().UtcDateTime;
        return assets.Select(a => ToSummary(a, now)).ToList();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    // Los decimales se ordenan como double: SQLite no sabe ordenar por decimal y para una lista
    // visual la precisión de double sobra. El Id desempata para que la paginación sea estable.
    private static IOrderedQueryable<Asset> Sort(IQueryable<Asset> q, AssetSort sort, bool desc) => sort switch
    {
        AssetSort.Name => desc
            ? q.OrderByDescending(a => a.Name).ThenBy(a => a.Id)
            : q.OrderBy(a => a.Name).ThenBy(a => a.Id),
        AssetSort.Price => desc
            ? q.OrderByDescending(a => (double?)a.Price!.PriceUsd).ThenBy(a => a.Id)
            : q.OrderBy(a => (double?)a.Price!.PriceUsd).ThenBy(a => a.Id),
        AssetSort.Change24h => desc
            ? q.OrderByDescending(a => (double?)a.Price!.Change24hPct).ThenBy(a => a.Id)
            : q.OrderBy(a => (double?)a.Price!.Change24hPct).ThenBy(a => a.Id),
        AssetSort.MarketCap => desc
            ? q.OrderByDescending(a => (double?)a.Price!.MarketCapUsd).ThenBy(a => a.Id)
            : q.OrderBy(a => (double?)a.Price!.MarketCapUsd).ThenBy(a => a.Id),
        AssetSort.Volume => desc
            ? q.OrderByDescending(a => (double?)a.Price!.Volume24hUsd).ThenBy(a => a.Id)
            : q.OrderBy(a => (double?)a.Price!.Volume24hUsd).ThenBy(a => a.Id),
        _ => desc
            ? q.OrderByDescending(a => a.Rank ?? int.MaxValue).ThenBy(a => a.Id)
            : q.OrderBy(a => a.Rank ?? int.MaxValue).ThenBy(a => a.Id)
    };

    private AssetSummary ToSummary(Asset a, DateTime nowUtc)
    {
        decimal? price = null;
        string? source = null;
        DateTime? updated = null;

        if (_cache.TryGet(a.Id, out var live)
            && nowUtc - live.UpdatedAtUtc <= TimeSpan.FromSeconds(Math.Max(1, _options.StaleSeconds)))
        {
            price = live.PriceUsd;
            source = PriceSources.Live;
            updated = live.UpdatedAtUtc;
        }
        else if (a.Price is not null)
        {
            price = a.Price.PriceUsd;
            source = PriceSources.Delayed;
            updated = a.Price.UpdatedAtUtc;
        }

        return new AssetSummary(
            a.Id, a.Slug, a.Symbol, a.Name, a.ImageUrl, a.Rank,
            price, source, updated,
            a.Price?.MarketCapUsd, a.Price?.Volume24hUsd,
            a.Price?.Change1hPct, a.Price?.Change24hPct, a.Price?.Change7dPct);
    }
}
