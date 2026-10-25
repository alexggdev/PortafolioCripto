using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Watchlist;
using CriptoPortfolio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CriptoPortfolio.Infrastructure.Watchlist;

public sealed class WatchlistService : IWatchlistService
{
    private readonly IAppDbContext _db;
    private readonly IAssetQueryService _assets;
    private readonly TimeProvider _time;

    public WatchlistService(IAppDbContext db, IAssetQueryService assets, TimeProvider time)
    {
        _db = db;
        _assets = assets;
        _time = time;
    }

    public async Task<IReadOnlyList<WatchlistItemDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _db.WatchlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAtUtc).ThenBy(w => w.AssetId)
            .Take(IWatchlistService.MaxItems)
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            return Array.Empty<WatchlistItemDto>();
        }

        var summaries = (await _assets.GetByIdsAsync(items.Select(i => i.AssetId).ToList(), ct))
            .ToDictionary(a => a.Id);

        return items
            .Where(i => summaries.ContainsKey(i.AssetId))
            .Select(i => new WatchlistItemDto(summaries[i.AssetId], i.AddedAtUtc))
            .ToList();
    }

    public async Task<WatchlistResult> AddAsync(Guid userId, string slug, CancellationToken ct = default)
    {
        var assetId = await FindActiveAssetIdAsync(slug, ct);
        if (assetId is null)
        {
            return WatchlistResult.Fail(WatchlistFailure.NotFound, "La moneda no existe.");
        }

        if (await ExistsAsync(userId, assetId.Value, ct))
        {
            return WatchlistResult.Ok();
        }

        if (await _db.WatchlistItems.CountAsync(w => w.UserId == userId, ct) >= IWatchlistService.MaxItems)
        {
            return WatchlistResult.Fail(
                WatchlistFailure.LimitReached,
                $"Solo puedes seguir hasta {IWatchlistService.MaxItems} monedas.");
        }

        _db.WatchlistItems.Add(new WatchlistItem
        {
            UserId = userId,
            AssetId = assetId.Value,
            AddedAtUtc = _time.GetUtcNow().UtcDateTime
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dos peticiones a la vez (doble clic): la otra ya la guardó y el resultado es el mismo.
            // Si no existe, el fallo fue otro y se propaga. (No se puede usar await en un filtro "when".)
            if (!await ExistsAfterConflictAsync(userId, assetId.Value, ct))
            {
                throw;
            }
        }

        return WatchlistResult.Ok();
    }

    public async Task RemoveAsync(Guid userId, string slug, CancellationToken ct = default)
    {
        var normalized = Normalize(slug);
        if (normalized is null)
        {
            return;
        }

        // Sin filtrar por IsActive: si la moneda se desactivó, el usuario aún debe poder quitarla.
        var assetId = await _db.Assets.AsNoTracking()
            .Where(a => a.Slug == normalized)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (assetId is null)
        {
            return;
        }

        await _db.WatchlistItems
            .Where(w => w.UserId == userId && w.AssetId == assetId.Value)
            .ExecuteDeleteAsync(ct);
    }

    private Task<bool> ExistsAsync(Guid userId, int assetId, CancellationToken ct) =>
        _db.WatchlistItems.AsNoTracking().AnyAsync(w => w.UserId == userId && w.AssetId == assetId, ct);

    private async Task<bool> ExistsAfterConflictAsync(Guid userId, int assetId, CancellationToken ct)
    {
        // El contexto sigue con la entidad rechazada: se descarta para poder consultar sin arrastrarla.
        foreach (var entry in _db.ChangeTracker.Entries<WatchlistItem>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        return await ExistsAsync(userId, assetId, ct);
    }

    private async Task<int?> FindActiveAssetIdAsync(string slug, CancellationToken ct)
    {
        var normalized = Normalize(slug);
        if (normalized is null)
        {
            return null;
        }

        return await _db.Assets.AsNoTracking()
            .Where(a => a.IsActive && a.Slug == normalized)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static string? Normalize(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var value = slug.Trim().ToLowerInvariant();
        return value.Length > 200 ? null : value;
    }
}
