namespace CriptoPortfolio.Application.Catalog;

public enum AssetSort
{
    Rank,
    Name,
    Price,
    Change24h,
    MarketCap,
    Volume
}

/// <summary>Parámetros de la lista del catálogo. El servicio corrige los valores fuera de rango.</summary>
public sealed record AssetListQuery(
    string? Search = null,
    AssetSort Sort = AssetSort.Rank,
    bool Descending = false,
    int Page = 1,
    int PageSize = 50)
{
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 50;
}

public static class PriceSources
{
    /// <summary>Precio de Binance, de hace unos segundos.</summary>
    public const string Live = "live";

    /// <summary>Último precio de CoinGecko guardado en SQL (se usa si no hay precio en vivo reciente).</summary>
    public const string Delayed = "delayed";
}

/// <summary>Moneda del catálogo con su precio actual. Es lo que devuelve la API pública.</summary>
public sealed record AssetSummary(
    int Id,
    string Slug,
    string Symbol,
    string Name,
    string? ImageUrl,
    int? Rank,
    decimal? PriceUsd,
    string? PriceSource,
    DateTime? PriceUpdatedAtUtc,
    decimal? MarketCapUsd,
    decimal? Volume24hUsd,
    decimal? Change1hPct,
    decimal? Change24hPct,
    decimal? Change7dPct);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
