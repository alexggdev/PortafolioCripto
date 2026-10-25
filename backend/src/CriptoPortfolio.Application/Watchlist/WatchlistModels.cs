using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Watchlist;

public sealed record WatchlistItemDto(AssetSummary Asset, DateTime AddedAtUtc);

public enum WatchlistFailure
{
    None,
    NotFound,
    LimitReached
}

public sealed record WatchlistResult(bool Succeeded, WatchlistFailure Failure, string? Error)
{
    public static WatchlistResult Ok() => new(true, WatchlistFailure.None, null);
    public static WatchlistResult Fail(WatchlistFailure failure, string error) => new(false, failure, error);
}

/// <summary>La lista de seguimiento del usuario. Solo usa SQL y la caché en memoria de precios.</summary>
public interface IWatchlistService
{
    public const int MaxItems = 100;

    /// <summary>Monedas seguidas, la más reciente primero. Se omiten las monedas ya inactivas.</summary>
    Task<IReadOnlyList<WatchlistItemDto>> ListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Idempotente: seguir una moneda que ya se sigue no es un error.</summary>
    Task<WatchlistResult> AddAsync(Guid userId, string slug, CancellationToken ct = default);

    /// <summary>Idempotente: dejar de seguir una moneda que no se seguía no es un error.</summary>
    Task RemoveAsync(Guid userId, string slug, CancellationToken ct = default);
}
